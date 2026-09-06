using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Tests.Persistence;

[Collection(MySqlInfrastructureCollection.Name)]
public sealed class MySqlJobRunLifecycleConcurrencyTests(MySqlFixture fixture) : IAsyncLifetime
{
    private readonly MySqlFixture _fixture = fixture;
    private const string TestDatabase = "online_supermarket_job_lifecycle_tests";

    private DbContextOptions<AppDbContext> Options =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(_fixture.CreateDatabaseConnectionString(TestDatabase))
            .Options;

    private AppDbContext CreateContext() => new(Options);
    private static JobRunStore CreateStore(AppDbContext db) => new(db, TimeProvider.System);

    public async Task InitializeAsync()
    {
        await using var master = new MySqlConnection(_fixture.MasterConnectionString);
        await master.OpenAsync();
        await using (var drop = new MySqlCommand("DROP DATABASE IF EXISTS " + TestDatabase + ";", master))
            await drop.ExecuteNonQueryAsync();
        await using (var create = new MySqlCommand("CREATE DATABASE " + TestDatabase + " CHARACTER SET utf8mb4;", master))
            await create.ExecuteNonQueryAsync();
        await using var db = new AppDbContext(Options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<Guid> SeedQueuedAsync(DbContextOptions<AppDbContext> options, string key)
    {
        await using var db = new AppDbContext(options);
        var run = new BackgroundJobRun("LifecycleJob", key, DateTime.UtcNow.AddHours(-1));
        db.BackgroundJobRuns.Add(run);
        await db.SaveChangesAsync();
        return run.Id;
    }

    private static async Task<Guid> SeedRunningAsync(DbContextOptions<AppDbContext> options, string key, string token, DateTime leaseUntil)
    {
        await using var db = new AppDbContext(options);
        var run = new BackgroundJobRun("LifecycleJob", key, DateTime.UtcNow.AddHours(-1));
        run.Start(token, DateTime.UtcNow.AddHours(-1), leaseUntil);
        db.BackgroundJobRuns.Add(run);
        await db.SaveChangesAsync();
        return run.Id;
    }

    [Fact]
    public async Task Claim_Race_ExactlyOneOwnerWins()
    {
        var runId = await SeedQueuedAsync(Options, "Key-Race");

        var t1 = Task.Run(async () =>
        {
            await using var c = CreateContext();
            return await CreateStore(c).TryClaimAsync(runId, "t1", DateTime.UtcNow.AddMinutes(10), CancellationToken.None);
        });
        var t2 = Task.Run(async () =>
        {
            await using var c = CreateContext();
            return await CreateStore(c).TryClaimAsync(runId, "t2", DateTime.UtcNow.AddMinutes(10), CancellationToken.None);
        });

        var outcomes = await Task.WhenAll(t1, t2);
        Assert.Equal(1, outcomes.Count(x => x));

        await using var verify = CreateContext();
        var row = await verify.BackgroundJobRuns.AsNoTracking().SingleAsync(x => x.Id == runId);
        Assert.Equal(JobRunStatus.Running, row.Status);
        Assert.True(row.LockToken == "t1" || row.LockToken == "t2");
        Assert.Equal(1, await verify.BackgroundJobRuns.CountAsync(x => x.Status == JobRunStatus.Running && x.LockKey == "Key-Race"));
    }

    [Fact]
    public async Task Renew_OnlyUnexpiredOwnerSucceeds()
    {
        var runId = await SeedRunningAsync(Options, "Key-Renew", "owner", DateTime.UtcNow.AddMinutes(5));

        await using (var owner = CreateContext())
            Assert.True(await CreateStore(owner).TryRenewAsync(runId, "owner", DateTime.UtcNow.AddMinutes(15), CancellationToken.None));

        await using (var intruder = CreateContext())
            Assert.False(await CreateStore(intruder).TryRenewAsync(runId, "wrong", DateTime.UtcNow.AddMinutes(15), CancellationToken.None));

        var expiredId = await SeedRunningAsync(Options, "Key-Expired", "owner2", DateTime.UtcNow.AddMinutes(-1));
        await using (var expired = CreateContext())
            Assert.False(await CreateStore(expired).TryRenewAsync(expiredId, "owner2", DateTime.UtcNow.AddMinutes(15), CancellationToken.None));
    }

    [Fact]
    public async Task FailExpired_Race_FailsOnce()
    {
        var runId = await SeedRunningAsync(Options, "Key-Stale", "stale-owner", DateTime.UtcNow.AddMinutes(-1));

        var t1 = Task.Run(async () =>
        {
            await using var c = CreateContext();
            return await CreateStore(c).FailExpiredAsync(DateTime.UtcNow, "Lease expired", CancellationToken.None);
        });
        var t2 = Task.Run(async () =>
        {
            await using var c = CreateContext();
            return await CreateStore(c).FailExpiredAsync(DateTime.UtcNow, "Lease expired", CancellationToken.None);
        });

        var counts = await Task.WhenAll(t1, t2);
        Assert.Equal(1, counts.Sum());

        await using var verify = CreateContext();
        var row = await verify.BackgroundJobRuns.AsNoTracking().SingleAsync(x => x.Id == runId);
        Assert.Equal(JobRunStatus.Failed, row.Status);
        Assert.Equal("Lease expired", row.ErrorSummary);
        Assert.Null(row.LockToken);
        Assert.Equal($"released:{runId}", row.LockKey);
    }

    [Fact]
    public async Task Complete_Success_ReleasesLockSlot()
    {
        var runId = await SeedRunningAsync(Options, "Key-Done", "done", DateTime.UtcNow.AddMinutes(5));

        await using (var c = CreateContext())
            Assert.True(await CreateStore(c).TryCompleteAsync(runId, "done", true, null, DateTime.UtcNow, CancellationToken.None));

        await using var verify = CreateContext();
        var row = await verify.BackgroundJobRuns.AsNoTracking().SingleAsync(x => x.Id == runId);
        Assert.Equal(JobRunStatus.Succeeded, row.Status);
        Assert.Equal($"released:{runId}", row.LockKey);
        Assert.Null(row.LockToken);
    }
}