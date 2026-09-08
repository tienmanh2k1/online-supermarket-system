using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Recommendations;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;
using OnlineSupermarket.Infrastructure.Recommendations;

namespace OnlineSupermarket.Infrastructure.Tests.Recommendations;

[Collection("MfScorerTests")]
public sealed class RecommendationJobHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly RecommendationJobHandler _handler;

    private Guid _userId;
    private Guid _productAId;
    private Guid _productBId;
    private Guid _inactiveProductId;

    public RecommendationJobHandlerTests()
    {
        MfScorer.ClearModel();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new RecommendationJobHandler(
            _db,
            TimeProvider.System,
            Options.Create(new IntelligenceJobsOptions { RecommendationIntervalMinutes = 60 }));
    }

    public void Dispose()
    {
        MfScorer.ClearModel();
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<Guid> SeedRunAsync()
    {
        await _db.BackgroundJobRuns
            .Where(run => run.JobName == "Recommendations" && run.LockKey == "global")
            .ExecuteUpdateAsync(run => run.SetProperty(r => r.LockKey, "released:" + Guid.NewGuid()));

        var row = new BackgroundJobRun("Recommendations", "global", DateTime.UtcNow);
        row.Start("handler-token", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(10));
        _db.BackgroundJobRuns.Add(row);
        await _db.SaveChangesAsync();
        return row.Id;
    }

    private async Task SeedCatalogAndSignalsAsync()
    {
        var branch = new Branch("View Branch", "1 Test Street", "0100000000", 10m, 106m);
        var category = new Category("Cats", "cats");
        var brand = new Brand("BrandA", "brand-a");
        var user = User.Create($"user_{Guid.NewGuid():N}@test.com", "hash", "User", null);
        _db.Branches.Add(branch);
        _db.Categories.Add(category);
        _db.Brands.Add(brand);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _userId = user.Id;

        var productA = new Product(category.Id, brand.Id, "SKU-A", "Product A", "product-a",
            "desc", 50_000m, "cái", null);
        var productB = new Product(category.Id, brand.Id, "SKU-B", "Product B", "product-b",
            "desc", 60_000m, "cái", null);
        var inactive = new Product(category.Id, brand.Id, "SKU-C", "Inactive", "inactive-c",
            "desc", 70_000m, "cái", null);
        inactive.Deactivate();
        _db.Products.Add(productA);
        _db.Products.Add(productB);
        _db.Products.Add(inactive);
        await _db.SaveChangesAsync();

        _productAId = productA.Id;
        _productBId = productB.Id;
        _inactiveProductId = inactive.Id;

        var order = Order.Create(
            user.Id,
            branch.Id,
            "Delivery",
            "Nguyen Van A",
            "0900000000",
            "123 Le Loi",
            null,
            [(productA.Id, "Product A", "SKU-A", productA.BasePrice, 2, productA.BasePrice * 2)],     
            productA.BasePrice * 2,
            0,
            0,
            productA.BasePrice * 2);
        order.SetStatus(OrderStatus.Completed, "Completed");
        _db.Orders.Add(order);

        _db.ProductViewEvents.Add(ProductViewEvent.Create(
            productA.Id, user.Id, null, branch.Id, DateTime.UtcNow));
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task ExecuteAsync_MaterializesAllThreeScopesInOneRun()
    {
        await SeedCatalogAndSignalsAsync();
        var runId = await SeedRunAsync();

        await _handler.HandleAsync(runId, CancellationToken.None);

        var rows = await _db.RecommendationResults
            .Where(x => x.JobRunId == runId)
            .ToListAsync();

        Assert.Contains(rows, x => x.Scope == RecommendationScope.Global);
        Assert.Contains(rows, x => x.Scope == RecommendationScope.User && x.UserId == _userId);
        Assert.Contains(rows, x => x.Scope == RecommendationScope.SimilarProduct && x.SourceProductId != null);
        Assert.DoesNotContain(rows, x => x.RecommendedProductId == _inactiveProductId || x.SourceProductId == _inactiveProductId);
    }

    [Fact]
    public async Task ExecuteAsync_NewRunPreservesPriorHistory()
    {
        await SeedCatalogAndSignalsAsync();
        var firstRunId = await SeedRunAsync();
        var secondRunId = await SeedRunAsync();

        await _handler.HandleAsync(firstRunId, CancellationToken.None);
        await _handler.HandleAsync(secondRunId, CancellationToken.None);

        var firstCount = await _db.RecommendationResults.CountAsync(x => x.JobRunId == firstRunId);
        var secondCount = await _db.RecommendationResults.CountAsync(x => x.JobRunId == secondRunId);

        Assert.True(firstCount > 0);
        Assert.True(secondCount > 0);
    }

    [Fact]
    public async Task ExecuteAsync_ExpiryIsIntervalPlusGrace()
    {
        await SeedCatalogAndSignalsAsync();
        var runId = await SeedRunAsync();

        await _handler.HandleAsync(runId, CancellationToken.None);

        var rows = await _db.RecommendationResults
            .Where(x => x.JobRunId == runId)
            .ToListAsync();

        Assert.True(rows.Count > 0);
        foreach (var row in rows)
        {
            Assert.True(row.ExpiresAtUtc - row.GeneratedAtUtc >= TimeSpan.FromMinutes(74));
            Assert.True(row.ExpiresAtUtc - row.GeneratedAtUtc <= TimeSpan.FromMinutes(76));
        }
    }

    [Fact]
    public async Task ExecuteAsync_ProducesSequentialRanksPerAudience()
    {
        await SeedCatalogAndSignalsAsync();
        var runId = await SeedRunAsync();

        await _handler.HandleAsync(runId, CancellationToken.None);

        var rows = await _db.RecommendationResults
            .Where(x => x.JobRunId == runId)
            .ToListAsync();

        foreach (var entry in rows.GroupBy(x => x.AudienceKey))
        {
            var ranks = entry.Select(x => x.Rank).OrderBy(x => x).ToArray();
            Assert.Equal(Enumerable.Range(1, ranks.Length).ToArray(), ranks);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithConflict_RollsBackTheWholeBatch()
    {
        await SeedCatalogAndSignalsAsync();
        var runId = await SeedRunAsync();

        _db.RecommendationResults.Add(RecommendationResult.CreateGlobal(
            _productAId, 0.5m, 1, "Existing", "content-v1",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(2), runId));
        await _db.SaveChangesAsync();

        var exception = await Record.ExceptionAsync(() =>
            _handler.HandleAsync(runId, CancellationToken.None));

        Assert.NotNull(exception);
        var count = await _db.RecommendationResults.CountAsync(x => x.JobRunId == runId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoSignals_ProducesNoRows()
    {
        var branch = new Branch("Empty Branch", "1 Test Street", "0100000000", 10m, 106m);
        var category = new Category("Cats", "cats");
        var brand = new Brand("BrandA", "brand-a");
        _db.Branches.Add(branch);
        _db.Categories.Add(category);
        _db.Brands.Add(brand);
        var product = new Product(category.Id, brand.Id, "SKU-A", "Product A", "product-a",
            "desc", 50_000m, "cái", null);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var runId = await SeedRunAsync();
        await _handler.HandleAsync(runId, CancellationToken.None);

        var count = await _db.RecommendationResults.CountAsync(x => x.JobRunId == runId);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTrainingFails_CleansUpModelAndThrows_PreservingPriorBatch()
    {
        await SeedCatalogAndSignalsAsync();
        var firstRunId = await SeedRunAsync();
        await _handler.HandleAsync(firstRunId, CancellationToken.None);

        var firstCount = await _db.RecommendationResults.CountAsync(x => x.JobRunId == firstRunId);
        Assert.True(firstCount > 0);

        var failingRunId = await SeedRunAsync();
        _handler.EnsureModelSeam = _ => throw new InvalidOperationException("Simulated MF trainer failure");

        var exception = await Record.ExceptionAsync(() =>
            _handler.HandleAsync(failingRunId, CancellationToken.None));

        Assert.NotNull(exception);
        Assert.Equal("Simulated MF trainer failure", exception.Message);

        // Model state must be cleaned up
        Assert.False(MfScorer.IsModelTrained);

        // Prior batch rows are completely preserved
        var postFirstCount = await _db.RecommendationResults.CountAsync(x => x.JobRunId == firstRunId);
        Assert.Equal(firstCount, postFirstCount);

        // No rows recorded for the failing run
        var failingCount = await _db.RecommendationResults.CountAsync(x => x.JobRunId == failingRunId);
        Assert.Equal(0, failingCount);
    }

    private async Task SeedSufficientSignalsForMfAsync()
    {
        var branch = await _db.Branches.FirstAsync();
        var category = await _db.Categories.FirstAsync();
        var brand = await _db.Brands.FirstAsync();

        var users = Enumerable.Range(1, 4)
            .Select(i => User.Create($"mfuser{i}_{Guid.NewGuid():N}@test.com", "hash", $"MF User {i}", null))
            .ToList();
        _db.Users.AddRange(users);

        var products = Enumerable.Range(1, 6)
            .Select(i => new Product(category.Id, brand.Id, $"SKU-MF-{i}", $"MF Product {i}", $"mf-product-{i}", "desc", 50_000m + i * 10_000m, "cái", null))
            .ToList();
        _db.Products.AddRange(products);
        await _db.SaveChangesAsync();

        var allUsers = new[] { _userId }.Concat(users.Select(u => u.Id)).ToList();
        var allProducts = new[] { _productAId, _productBId }.Concat(products.Select(p => p.Id)).ToList();

        for (var uIdx = 0; uIdx < allUsers.Count; uIdx++)
        {
            for (var pIdx = 0; pIdx < 3; pIdx++)
            {
                var prodId = allProducts[(uIdx + pIdx) % allProducts.Count];
                _db.ProductViewEvents.Add(ProductViewEvent.Create(
                    prodId, allUsers[uIdx], null, branch.Id, DateTime.UtcNow.AddMinutes(-10)));
            }
        }

        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPredictionFailsOnTrainedModel_FallsBackToContentWithoutFakingMf()
    {
        await SeedCatalogAndSignalsAsync();
        await SeedSufficientSignalsForMfAsync();
        var runId = await SeedRunAsync();

        MfScorer.ClearModel();

        try
        {
            // Inject an exception at prediction boundary on a genuinely trained MF model
            MfScorer.ScoreSeam = (_, _, _) => throw new InvalidOperationException("Simulated ML transform/prediction failure");

            await _handler.HandleAsync(runId, CancellationToken.None);

            // Verify model was genuinely trained by EnsureModel during run
            Assert.True(MfScorer.IsModelTrained);

            var userRows = await _db.RecommendationResults
                .Where(x => x.JobRunId == runId && x.Scope == RecommendationScope.User)
                .ToListAsync();

            Assert.NotEmpty(userRows);
            Assert.All(userRows, row =>
            {
                Assert.Equal("content-v1", row.AlgorithmVersion);
                Assert.Equal("Phù hợp danh mục đã xem", row.Reason);
                Assert.NotEqual("Gợi ý từ mô hình AI", row.Reason);
            });
        }
        finally
        {
            MfScorer.ClearModel();
        }
    }

    [Fact]
    public async Task ExecuteAsync_ViaJobRunExecutor_WhenTrainingFails_MarksRunFailedInStore()
    {
        await SeedCatalogAndSignalsAsync();
        var runId = await SeedRunAsync();

        _handler.EnsureModelSeam = _ => throw new InvalidOperationException("Simulated MF trainer failure");

        var storeMock = new Mock<IJobRunStore>();
        string? recordedError = null;
        storeMock.Setup(s => s.TryStartAsync(runId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        storeMock.Setup(s => s.TryFailAsync(runId, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, string?, DateTime, CancellationToken>((_, _, err, _, _) => recordedError = err)
            .ReturnsAsync(true);

        var services = new ServiceCollection();
        services.AddScoped<IJobRunStore>(_ => storeMock.Object);
        services.AddScoped<IBackgroundJobHandler>(_ => _handler);
        var sp = services.BuildServiceProvider();

        var executor = new JobRunExecutor(
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new IntelligenceJobsOptions { LeaseMinutes = 5 }),
            NullLogger<JobRunExecutor>.Instance);

        await executor.ExecuteAsync(new JobRequest(runId, "Recommendations"), CancellationToken.None);

        storeMock.Verify(s => s.TryFailAsync(runId, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(recordedError);
        Assert.Contains("Simulated MF trainer failure", recordedError);
    }
}