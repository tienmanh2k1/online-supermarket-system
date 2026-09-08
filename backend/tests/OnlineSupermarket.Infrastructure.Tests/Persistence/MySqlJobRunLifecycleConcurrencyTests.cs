using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Domain.Recommendations;
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
    private static EfJobRunStore CreateStore(DbContextOptions<AppDbContext> options)
        => new(new OnlineSupermarket.Infrastructure.Tests.Jobs.TestDbContextFactory(options));

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

    private static async Task<Guid> SeedProductAsync(DbContextOptions<AppDbContext> options)
    {
        await using var db = new AppDbContext(options);
        var category = new Category("TestCategory", "test-category-" + Guid.NewGuid().ToString("N"));
        var brand = new Brand("TestBrand", "test-brand-" + Guid.NewGuid().ToString("N"));
        db.Categories.Add(category);
        db.Brands.Add(brand);
        var product = new Product(
            category.Id,
            brand.Id,
            "SKU-" + Guid.NewGuid().ToString("N")[..8],
            "Test Product",
            "test-product-" + Guid.NewGuid().ToString("N"),
            "desc",
            10000m,
            "item",
            null);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task PublishBatchAsync(AppDbContext db, Guid runId, Guid productId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            await JobRunPublishGuard.EnsureOwnedAsync(db, runId, TimeProvider.System, CancellationToken.None);
            db.RecommendationResults.Add(RecommendationResult.CreateGlobal(
                productId,
                0.85m,
                1,
                "Top Pick",
                "v1",
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(2),
                runId));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    [Fact]
    public async Task Claim_Race_ExactlyOneOwnerWins()
    {
        var runId = await SeedQueuedAsync(Options, "Key-Race");

        var t1 = Task.Run(async () =>
        {
            await using var c = CreateContext();
            return await CreateStore(Options).TryStartAsync(runId, "t1", DateTime.UtcNow.AddMinutes(10), CancellationToken.None);
        });
        var t2 = Task.Run(async () =>
        {
            await using var c = CreateContext();
            return await CreateStore(Options).TryStartAsync(runId, "t2", DateTime.UtcNow.AddMinutes(10), CancellationToken.None);
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
            Assert.True(await CreateStore(Options).TryRenewAsync(runId, "owner", DateTime.UtcNow.AddMinutes(15), CancellationToken.None));

        await using (var intruder = CreateContext())
            Assert.False(await CreateStore(Options).TryRenewAsync(runId, "wrong", DateTime.UtcNow.AddMinutes(15), CancellationToken.None));

        var expiredId = await SeedRunningAsync(Options, "Key-Expired", "owner2", DateTime.UtcNow.AddMinutes(-1));
        await using (var expired = CreateContext())
            Assert.False(await CreateStore(Options).TryRenewAsync(expiredId, "owner2", DateTime.UtcNow.AddMinutes(15), CancellationToken.None));
    }

    [Fact]
    public async Task FailExpired_Race_FailsOnce()
    {
        var runId = await SeedRunningAsync(Options, "Key-Stale", "stale-owner", DateTime.UtcNow.AddMinutes(-1));

        var t1 = Task.Run(async () =>
        {
            await using var c = CreateContext();
            return await CreateStore(Options).FailExpiredAsync(DateTime.UtcNow, "Lease expired", CancellationToken.None);
        });
        var t2 = Task.Run(async () =>
        {
            await using var c = CreateContext();
            return await CreateStore(Options).FailExpiredAsync(DateTime.UtcNow, "Lease expired", CancellationToken.None);
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
            Assert.True(await CreateStore(Options).TrySucceedAsync(runId, "done", DateTime.UtcNow, CancellationToken.None));

        await using var verify = CreateContext();
        var row = await verify.BackgroundJobRuns.AsNoTracking().SingleAsync(x => x.Id == runId);
        Assert.Equal(JobRunStatus.Succeeded, row.Status);
        Assert.Equal($"released:{runId}", row.LockKey);
        Assert.Null(row.LockToken);
    }

    [Fact]
    public async Task PublishGuard_WhenLeaseLostOrExpired_AbortsPublishOnMySql()
    {
        var productId = await SeedProductAsync(Options);

        // 1. Live unexpired run: EnsureOwnedAsync passes on MySQL FOR UPDATE query and publishes batch
        var liveRunId = await SeedRunningAsync(Options, "Key-LivePublish", "token-live", DateTime.UtcNow.AddMinutes(5));
        await using (var db = CreateContext())
        {
            await PublishBatchAsync(db, liveRunId, productId);
        }
        await using (var verifyDb = CreateContext())
        {
            Assert.Equal(1, await verifyDb.RecommendationResults.CountAsync(x => x.JobRunId == liveRunId));
        }

        // 2. Expired lease run: EnsureOwnedAsync throws OperationCanceledException and aborts publish
        var expiredRunId = await SeedRunningAsync(Options, "Key-ExpiredPublish", "token-expired", DateTime.UtcNow.AddMinutes(-2));
        await using (var db = CreateContext())
        {
            var ex = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                PublishBatchAsync(db, expiredRunId, productId));
            Assert.Contains("Job run is no longer owned", ex.Message);
        }
        await using (var verifyDb = CreateContext())
        {
            Assert.Equal(0, await verifyDb.RecommendationResults.CountAsync(x => x.JobRunId == expiredRunId));
        }

        // 3a. Terminal Failed run: store transitions Running -> Failed, guard rejects and aborts publish
        var failedRunId = await SeedRunningAsync(Options, "Key-TerminalFailedPublish", "token-fail", DateTime.UtcNow.AddMinutes(5));
        var failed = await CreateStore(Options).TryFailAsync(failedRunId, "token-fail", "Abandoned", DateTime.UtcNow, CancellationToken.None);
        Assert.True(failed);
        await using (var verifyDb = CreateContext())
        {
            var row = await verifyDb.BackgroundJobRuns.AsNoTracking().SingleAsync(x => x.Id == failedRunId);
            Assert.Equal(JobRunStatus.Failed, row.Status);
        }
        await using (var db = CreateContext())
        {
            var ex = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                PublishBatchAsync(db, failedRunId, productId));
            Assert.Contains("Job run is no longer owned", ex.Message);
        }
        await using (var verifyDb = CreateContext())
        {
            Assert.Equal(0, await verifyDb.RecommendationResults.CountAsync(x => x.JobRunId == failedRunId));
        }

        // 3b. Terminal Succeeded run: store transitions Running -> Succeeded, guard rejects and aborts publish
        var succeededRunId = await SeedRunningAsync(Options, "Key-TerminalSucceededPublish", "token-succeed", DateTime.UtcNow.AddMinutes(5));
        var succeeded = await CreateStore(Options).TrySucceedAsync(succeededRunId, "token-succeed", DateTime.UtcNow, CancellationToken.None);
        Assert.True(succeeded);
        await using (var verifyDb = CreateContext())
        {
            var row = await verifyDb.BackgroundJobRuns.AsNoTracking().SingleAsync(x => x.Id == succeededRunId);
            Assert.Equal(JobRunStatus.Succeeded, row.Status);
        }
        await using (var db = CreateContext())
        {
            var ex = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                PublishBatchAsync(db, succeededRunId, productId));
            Assert.Contains("Job run is no longer owned", ex.Message);
        }
        await using (var verifyDb = CreateContext())
        {
            Assert.Equal(0, await verifyDb.RecommendationResults.CountAsync(x => x.JobRunId == succeededRunId));
        }
    }
}