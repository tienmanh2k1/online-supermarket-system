using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;
using Xunit;

namespace OnlineSupermarket.Infrastructure.Tests.Jobs;

public sealed class JobRunStoreTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly EfJobRunStore _sut;
    private static readonly DateTime Now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

    public JobRunStoreTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        _sut = new EfJobRunStore(new TestDbContextFactory(options));
    }

    private async Task<BackgroundJobRun> SeedRunAsync(string lockKey = "Key1", JobRunStatus? status = null)
    {
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options);
        var run = new BackgroundJobRun("TestJob", lockKey, Now.AddHours(-1));
        if (status is not null)
        {
            run.GetType().GetProperty(nameof(BackgroundJobRun.Status))!.SetValue(run, status.Value);
        }
        db.BackgroundJobRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }

    private async Task<BackgroundJobRun> SeedRunningAsync(string lockKey, string token, DateTime leaseUntil)
    {
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options);
        var run = new BackgroundJobRun("TestJob", lockKey, Now.AddHours(-1));
        run.Start(token, Now.AddHours(-1), leaseUntil);
        db.BackgroundJobRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }

    private AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options);

    [Fact]
    public async Task Start_QueuedRun_TransitionsToRunningAndGrantsToken()
    {
        var run = await SeedRunAsync();

        var started = await _sut.TryStartAsync(run.Id, "lease-1", Now.AddMinutes(10), CancellationToken.None);

        Assert.True(started);
        await using var db = CreateContext();
        var row = await db.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(JobRunStatus.Running, row.Status);
        Assert.Equal("lease-1", row.LockToken);
        Assert.NotNull(row.StartedAtUtc);
        Assert.Equal(Now.AddMinutes(10), row.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task Start_SecondOwner_IsRejected()
    {
        var run = await SeedRunAsync();
        await _sut.TryStartAsync(run.Id, "lease-1", Now.AddMinutes(10), CancellationToken.None);

        var second = await _sut.TryStartAsync(run.Id, "lease-2", Now.AddMinutes(10), CancellationToken.None);

        Assert.False(second);
        await using var db = CreateContext();
        var row = await db.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal("lease-1", row.LockToken);
    }

    [Fact]
    public async Task Start_TerminalOrMissingRun_IsRejected()
    {
        var succeeded = await SeedRunningAsync("KeyS", "t", Now.AddMinutes(10));
        await using (var db = CreateContext())
        {
            var tracked = db.BackgroundJobRuns.Single(x => x.Id == succeeded.Id);
            tracked.MarkAsSucceeded("t", Now);
            await db.SaveChangesAsync();
        }

        Assert.False(await _sut.TryStartAsync(succeeded.Id, "again", Now.AddMinutes(10), CancellationToken.None));
        Assert.False(await _sut.TryStartAsync(Guid.NewGuid(), "again", Now.AddMinutes(10), CancellationToken.None));
    }

    [Fact]
    public async Task Renew_OwnerWithUnexpiredLease_Succeeds()
    {
        var run = await SeedRunningAsync("Key2", "token-2", DateTime.UtcNow.AddMinutes(5));

        var renewed = await _sut.TryRenewAsync(run.Id, "token-2", DateTime.UtcNow.AddMinutes(15), CancellationToken.None);

        Assert.True(renewed);
        await using var db = CreateContext();
        var row = await db.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.NotNull(row.LeaseExpiresAtUtc);
        Assert.True(row.LeaseExpiresAtUtc.Value > DateTime.UtcNow.AddMinutes(10));
    }

    [Fact]
    public async Task Renew_WrongTokenOrExpiredLease_IsRejected()
    {
        var owned = await SeedRunningAsync("KeyA", "t-a", DateTime.UtcNow.AddMinutes(5));
        var expired = await SeedRunningAsync("KeyB", "t-b", DateTime.UtcNow.AddMinutes(-5));

        Assert.False(await _sut.TryRenewAsync(owned.Id, "wrong", DateTime.UtcNow.AddMinutes(15), CancellationToken.None));
        Assert.False(await _sut.TryRenewAsync(expired.Id, "t-b", DateTime.UtcNow.AddMinutes(15), CancellationToken.None));
    }

    [Fact]
    public async Task Succeed_ReleasesSlotAndClearsLease()
    {
        var run = await SeedRunningAsync("Key3", "token-3", DateTime.UtcNow.AddMinutes(5));

        var completed = await _sut.TrySucceedAsync(run.Id, "token-3", Now, CancellationToken.None);

        Assert.True(completed);
        await using var db = CreateContext();
        var row = await db.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(JobRunStatus.Succeeded, row.Status);
        Assert.Equal(Now, row.CompletedAtUtc);
        Assert.Equal($"released:{run.Id}", row.LockKey);
        Assert.Null(row.LockToken);
        Assert.Null(row.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task Fail_StoresSanitizedSummaryAndReleasesSlot()
    {
        var run = await SeedRunningAsync("Key4", "token-4", DateTime.UtcNow.AddMinutes(5));

        var completed = await _sut.TryFailAsync(run.Id, "token-4", "boom", Now, CancellationToken.None);

        Assert.True(completed);
        await using var db = CreateContext();
        var row = await db.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(JobRunStatus.Failed, row.Status);
        Assert.Equal("boom", row.ErrorSummary);
        Assert.Equal($"released:{run.Id}", row.LockKey);
        Assert.Null(row.LockToken);
    }

    [Fact]
    public async Task Terminal_WrongToken_IsRejectedAndStatePreserved()
    {
        var run = await SeedRunningAsync("Key5", "token-5", DateTime.UtcNow.AddMinutes(5));

        var succeeded = await _sut.TrySucceedAsync(run.Id, "wrong", Now, CancellationToken.None);
        var failed = await _sut.TryFailAsync(run.Id, "wrong", "nope", Now, CancellationToken.None);

        Assert.False(succeeded);
        Assert.False(failed);
        await using var db = CreateContext();
        var row = await db.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(JobRunStatus.Running, row.Status);
        Assert.Equal("token-5", row.LockToken);
    }

    [Fact]
    public async Task FailExpired_FailsOnlyExpiredAndReleased()
    {
        var expired = await SeedRunningAsync("KeyE1", "t-e1", DateTime.UtcNow.AddMinutes(-1));
        var active = await SeedRunningAsync("KeyE2", "t-e2", DateTime.UtcNow.AddMinutes(10));
        var queued = await SeedRunAsync("KeyE3");

        var failed = await _sut.FailExpiredAsync(DateTime.UtcNow, "lease expired", CancellationToken.None);

        Assert.Equal(1, failed);
        await using var db = CreateContext();
        var expiredRow = await db.BackgroundJobRuns.SingleAsync(x => x.Id == expired.Id);
        Assert.Equal(JobRunStatus.Failed, expiredRow.Status);
        Assert.Equal("lease expired", expiredRow.ErrorSummary);
        Assert.Equal($"released:{expired.Id}", expiredRow.LockKey);
        Assert.Equal(JobRunStatus.Running, (await db.BackgroundJobRuns.SingleAsync(x => x.Id == active.Id)).Status);
        Assert.Equal(JobRunStatus.Queued, (await db.BackgroundJobRuns.SingleAsync(x => x.Id == queued.Id)).Status);
    }

    [Fact]
    public async Task Fail_TruncatesLongErrorSummary()
    {
        var run = await SeedRunningAsync("Key6", "token-6", DateTime.UtcNow.AddMinutes(5));
        var longError = new string('x', 5000);

        await _sut.TryFailAsync(run.Id, "token-6", longError, Now, CancellationToken.None);

        await using var db = CreateContext();
        var row = await db.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(1000, row.ErrorSummary!.Length);
        Assert.EndsWith("...", row.ErrorSummary);
    }
}