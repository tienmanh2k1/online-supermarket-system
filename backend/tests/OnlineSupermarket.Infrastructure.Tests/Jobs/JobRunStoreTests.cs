using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;
using Xunit;

namespace OnlineSupermarket.Infrastructure.Tests.Jobs;

public sealed class JobRunStoreTests
{
    private readonly AppDbContext _dbContext;
    private readonly JobRunStore _sut;
    private static readonly DateTime Now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

    public JobRunStoreTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
        _sut = new JobRunStore(_dbContext, new FixedTimeProvider());
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private async Task<BackgroundJobRun> SeedRunAsync(string lockKey = "Key1", JobRunStatus? status = null)
    {
        var run = new BackgroundJobRun("TestJob", lockKey, Now.AddHours(-1));
        if (status is not null)
        {
            run.GetType().GetProperty(nameof(BackgroundJobRun.Status))!.SetValue(run, status.Value);
        }
        _dbContext.BackgroundJobRuns.Add(run);
        await _dbContext.SaveChangesAsync();
        return run;
    }

    private async Task<BackgroundJobRun> SeedRunningAsync(string lockKey, string token, DateTime leaseUntil)
    {
        var run = new BackgroundJobRun("TestJob", lockKey, Now.AddHours(-1));
        run.Start(token, Now.AddHours(-1), leaseUntil);
        _dbContext.BackgroundJobRuns.Add(run);
        await _dbContext.SaveChangesAsync();
        return run;
    }

    [Fact]
    public async Task Claim_QueuedRun_TransitionsToRunningAndGrantsToken()
    {
        var run = await SeedRunAsync();

        var claimed = await _sut.TryClaimAsync(run.Id, "lease-1", Now.AddMinutes(10), CancellationToken.None);

        Assert.True(claimed);
        var row = await _dbContext.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(JobRunStatus.Running, row.Status);
        Assert.Equal("lease-1", row.LockToken);
        Assert.NotNull(row.StartedAtUtc);
        Assert.Equal(Now.AddMinutes(10), row.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task Claim_SecondOwner_IsRejected()
    {
        var run = await SeedRunAsync();
        await _sut.TryClaimAsync(run.Id, "lease-1", Now.AddMinutes(10), CancellationToken.None);

        var second = await _sut.TryClaimAsync(run.Id, "lease-2", Now.AddMinutes(10), CancellationToken.None);

        Assert.False(second);
        var row = await _dbContext.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal("lease-1", row.LockToken);
    }

    [Fact]
    public async Task Claim_TerminalOrMissingRun_IsRejected()
    {
        var succeeded = await SeedRunningAsync("KeyS", "t", Now.AddMinutes(10));
        succeeded.MarkAsSucceeded("t", Now);
        await _dbContext.SaveChangesAsync();

        Assert.False(await _sut.TryClaimAsync(succeeded.Id, "again", Now.AddMinutes(10), CancellationToken.None));
        Assert.False(await _sut.TryClaimAsync(Guid.NewGuid(), "again", Now.AddMinutes(10), CancellationToken.None));
    }

    [Fact]
    public async Task Renew_OwnerWithUnexpiredLease_Succeeds()
    {
        var run = await SeedRunningAsync("Key2", "token-2", Now.AddMinutes(5));

        var renewed = await _sut.TryRenewAsync(run.Id, "token-2", Now.AddMinutes(15), CancellationToken.None);

        Assert.True(renewed);
        var row = await _dbContext.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(Now.AddMinutes(15), row.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task Renew_WrongTokenOrExpiredLease_IsRejected()
    {
        var owned = await SeedRunningAsync("KeyA", "t-a", Now.AddMinutes(5));
        var expired = await SeedRunningAsync("KeyB", "t-b", Now.AddMinutes(-5));

        Assert.False(await _sut.TryRenewAsync(owned.Id, "wrong", Now.AddMinutes(15), CancellationToken.None));
        Assert.False(await _sut.TryRenewAsync(expired.Id, "t-b", Now.AddMinutes(15), CancellationToken.None));
    }

    [Fact]
    public async Task Complete_Success_ReleasesSlotAndClearsLease()
    {
        var run = await SeedRunningAsync("Key3", "token-3", Now.AddMinutes(5));

        var completed = await _sut.TryCompleteAsync(run.Id, "token-3", true, null, Now, CancellationToken.None);

        Assert.True(completed);
        var row = await _dbContext.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(JobRunStatus.Succeeded, row.Status);
        Assert.Equal(Now, row.CompletedAtUtc);
        Assert.Equal($"released:{run.Id}", row.LockKey);
        Assert.Null(row.LockToken);
        Assert.Null(row.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task Complete_Failure_StoresSanitizedSummaryAndReleasesSlot()
    {
        var run = await SeedRunningAsync("Key4", "token-4", Now.AddMinutes(5));

        var completed = await _sut.TryCompleteAsync(run.Id, "token-4", false, "boom", Now, CancellationToken.None);

        Assert.True(completed);
        var row = await _dbContext.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(JobRunStatus.Failed, row.Status);
        Assert.Equal("boom", row.ErrorSummary);
        Assert.Equal($"released:{run.Id}", row.LockKey);
        Assert.Null(row.LockToken);
    }

    [Fact]
    public async Task Complete_WrongToken_IsRejectedAndStatePreserved()
    {
        var run = await SeedRunningAsync("Key5", "token-5", Now.AddMinutes(5));

        var completed = await _sut.TryCompleteAsync(run.Id, "wrong", true, null, Now, CancellationToken.None);

        Assert.False(completed);
        var row = await _dbContext.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(JobRunStatus.Running, row.Status);
        Assert.Equal("token-5", row.LockToken);
    }

    [Fact]
    public async Task FailExpired_FailsOnlyExpiredAndReleased()
    {
        var expired = await SeedRunningAsync("KeyE1", "t-e1", Now.AddMinutes(-1));
        var active = await SeedRunningAsync("KeyE2", "t-e2", Now.AddMinutes(10));
        var queued = await SeedRunAsync("KeyE3");

        var failed = await _sut.FailExpiredAsync(Now, "lease expired", CancellationToken.None);

        Assert.Equal(1, failed);
        var expiredRow = await _dbContext.BackgroundJobRuns.SingleAsync(x => x.Id == expired.Id);
        Assert.Equal(JobRunStatus.Failed, expiredRow.Status);
        Assert.Equal("lease expired", expiredRow.ErrorSummary);
        Assert.Equal($"released:{expired.Id}", expiredRow.LockKey);
        Assert.Equal(JobRunStatus.Running, (await _dbContext.BackgroundJobRuns.SingleAsync(x => x.Id == active.Id)).Status);
        Assert.Equal(JobRunStatus.Queued, (await _dbContext.BackgroundJobRuns.SingleAsync(x => x.Id == queued.Id)).Status);
    }

    [Fact]
    public async Task Complete_Failure_TruncatesLongErrorSummary()
    {
        var run = await SeedRunningAsync("Key6", "token-6", Now.AddMinutes(5));
        var longError = new string('x', 5000);

        await _sut.TryCompleteAsync(run.Id, "token-6", false, longError, Now, CancellationToken.None);

        var row = await _dbContext.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(1000, row.ErrorSummary!.Length);
        Assert.EndsWith("...", row.ErrorSummary);
    }
}