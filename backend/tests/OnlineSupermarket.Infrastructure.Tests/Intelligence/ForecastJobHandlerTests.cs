using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Intelligence;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Intelligence;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Tests.Intelligence;

public sealed class ForecastJobHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly ForecastJobHandler _handler;
    private Guid _branchAId;
    private Guid _categoryId;
    private Guid _brandId;
    private bool _catalogSeeded;

    public ForecastJobHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _handler = new ForecastJobHandler(_db, TimeProvider.System);
    }

    private async Task<Guid> SeedCatalogAsync(Guid branchId)
    {
        Guid CreateProduct()
        {
            var product = new Product(_categoryId, _brandId,
                $"SKU-F-{Guid.NewGuid():N}".Substring(0, 32), "Forecast Product",
                $"forecast-product-{Guid.NewGuid():N}",
                "desc", 45_000m, "cái", null);
            _db.Products.Add(product);
            return product.Id;
        }

        if (!_catalogSeeded)
        {
            var category = new Category("ForecastCats", "forecast-cats");
            var brand = new Brand("ForecastBrand", "forecast-brand");
            _db.Categories.Add(category);
            _db.Brands.Add(brand);
            _categoryId = category.Id;
            _brandId = brand.Id;
            _catalogSeeded = true;
        }

        var productId = CreateProduct();
        await _db.SaveChangesAsync();

        var inventory = BranchInventory.Create(
            branchId, productId, 45_000m, 100, 5);
        _db.BranchInventories.Add(inventory);
        await _db.SaveChangesAsync();

        return inventory.Id;
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static async Task<Guid> SeedDailySalesAsync(
        AppDbContext db, Guid inventoryId, int days, int unitsPerDay)
    {
        for (var day = 0; day < days; day++)
        {
            AddSaleTransaction(db, inventoryId, unitsPerDay, DateTime.UtcNow.AddDays(-(day + 1)));
        }

        await db.SaveChangesAsync();
        return inventoryId;
    }

    private static void AddSaleTransaction(
        AppDbContext db, Guid inventoryId, int quantity, DateTime createdAtUtc)
    {
        var transaction = InventoryTransaction.Create(
            inventoryId,
            InventoryTransactionType.Sale,
            -quantity,
            -quantity,
            100 - quantity,
            0,
            InventoryReferenceType.Order,
            Guid.NewGuid(),
            $"order:sale:{Guid.NewGuid():N}",
            null,
            null,
            createdAtUtc);
        db.InventoryTransactions.Add(transaction);
    }

    private static void AddReserveTransaction(
        AppDbContext db, Guid inventoryId, int quantity, DateTime createdAtUtc)
    {
        var transaction = InventoryTransaction.Create(
            inventoryId,
            InventoryTransactionType.Reserve,
            0,
            quantity,
            100,
            quantity,
            InventoryReferenceType.Order,
            Guid.NewGuid(),
            $"order:reserve:{Guid.NewGuid():N}",
            null,
            null,
            createdAtUtc);
        db.InventoryTransactions.Add(transaction);
    }

    private async Task<Guid> CreateRunAsync(Guid branchId)
    {
        var run = new BackgroundJobRun("Forecast", $"branch:{branchId}", DateTime.UtcNow);
        _db.BackgroundJobRuns.Add(run);
        await _db.SaveChangesAsync();
        return run.Id;
    }

    private async Task CompleteRunAsync(Guid runId)
    {
        var run = await _db.BackgroundJobRuns.SingleAsync(run => run.Id == runId);
        var token = Guid.NewGuid().ToString();
        run.Start(token, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(9));
        run.MarkAsSucceeded(token, DateTime.UtcNow);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WritesTwoForecastsPerInventory()
    {
        var branch = new Branch("Forecast Branch", "1 Test Street", "0100000000", 10m, 106m);
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();
        _branchAId = branch.Id;

        var inventoryId = await SeedCatalogAsync(_branchAId);
        await SeedDailySalesAsync(_db, inventoryId, 14, 2);
        var runId = await CreateRunAsync(_branchAId);

        await _handler.HandleAsync(runId, CancellationToken.None);

        var forecasts = await _db.DemandForecasts
            .Where(x => x.JobRunId == runId && x.BranchInventoryId == inventoryId)
            .ToListAsync();

        Assert.Equal(new[] { 7, 14 }, forecasts.Select(x => x.HorizonDays).Order().ToArray());
        Assert.All(forecasts, x => Assert.Equal(ForecastDataQuality.Sufficient, x.DataQuality));
        Assert.All(forecasts, x => Assert.True(x.PredictedQuantity > 0m));
        Assert.Equal("sma-v1", forecasts[0].AlgorithmVersion);
    }

    [Fact]
    public async Task ExecuteAsync_IsIsolatedToTheRunBranch()
    {
        var branchA = new Branch("Branch A", "1 Test Street", "0100000000", 10m, 106m);
        var branchB = new Branch("Branch B", "2 Test Street", "0100000000", 10m, 106m);
        _db.Branches.AddRange(branchA, branchB);
        await _db.SaveChangesAsync();
        _branchAId = branchA.Id;

        var inventoryAId = await SeedCatalogAsync(_branchAId);
        var inventoryBId = await SeedCatalogAsync(branchB.Id);
        await SeedDailySalesAsync(_db, inventoryAId, 7, 1);
        await SeedDailySalesAsync(_db, inventoryBId, 7, 1);
        var runId = await CreateRunAsync(_branchAId);

        await _handler.HandleAsync(runId, CancellationToken.None);

        var rows = await _db.DemandForecasts
            .Where(x => x.JobRunId == runId)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.All(rows, x => Assert.Equal(inventoryAId, x.BranchInventoryId));
        Assert.DoesNotContain(rows, x => x.BranchInventoryId == inventoryBId);
    }

    [Fact]
    public async Task ExecuteAsync_IgnoresReserveReleaseAndManualAdjustment()
    {
        var branch = new Branch("Ignore Branch", "1 Test Street", "0100000000", 10m, 106m);
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();
        _branchAId = branch.Id;

        var inventoryWithReserve = await SeedCatalogAsync(_branchAId);
        AddReserveTransaction(_db, inventoryWithReserve, 5, DateTime.UtcNow.AddDays(-2));
        var inventoryWithSales = await SeedCatalogAsync(_branchAId);
        await SeedDailySalesAsync(_db, inventoryWithSales, 7, 2);
        await _db.SaveChangesAsync();

        var runId = await CreateRunAsync(_branchAId);
        await _handler.HandleAsync(runId, CancellationToken.None);

        var rows = await _db.DemandForecasts
            .Where(x => x.JobRunId == runId)
            .ToListAsync();

        var reserveOnly = rows.Where(x => x.BranchInventoryId == inventoryWithReserve).ToList();
        Assert.Equal(2, reserveOnly.Count);
        Assert.All(reserveOnly, x =>
        {
            Assert.Equal(ForecastDataQuality.Insufficient, x.DataQuality);
            Assert.Equal(0m, x.PredictedQuantity);
            Assert.Equal(0, x.ActualDataDays);
        });

        var saleRows = rows.Where(x => x.BranchInventoryId == inventoryWithSales).ToList();
        Assert.Equal(2, saleRows.Count);
        Assert.All(saleRows, x => Assert.True(x.PredictedQuantity > 0m));
    }

    [Fact]
    public async Task ExecuteAsync_NoHistory_WritesZeroInsufficientRows()
    {
        var branch = new Branch("Empty Forecast Branch", "1 Test Street", "0100000000", 10m, 106m);
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();
        _branchAId = branch.Id;

        var inventoryId = await SeedCatalogAsync(_branchAId);
        var runId = await CreateRunAsync(_branchAId);

        await _handler.HandleAsync(runId, CancellationToken.None);

        var rows = await _db.DemandForecasts
            .Where(x => x.JobRunId == runId)
            .ToListAsync();

        Assert.Equal(new[] { 7, 14 }, rows.Select(x => x.HorizonDays).Order().ToArray());
        Assert.All(rows, x =>
        {
            Assert.Equal(0m, x.PredictedQuantity);
            Assert.Equal(0, x.ActualDataDays);
            Assert.Equal(ForecastDataQuality.Insufficient, x.DataQuality);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithConflict_RollsBackTheWholeBatch()
    {
        var branch = new Branch("Rollback Branch", "1 Test Street", "0100000000", 10m, 106m);
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();
        _branchAId = branch.Id;

        var inventoryId = await SeedCatalogAsync(_branchAId);
        await SeedDailySalesAsync(_db, inventoryId, 7, 2);
        var runId = await CreateRunAsync(_branchAId);

        _db.DemandForecasts.Add(DemandForecast.Create(
            inventoryId, 7, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1),
            12m, 7, ForecastDataQuality.Partial, "sma-v1", DateTime.UtcNow, runId));
        await _db.SaveChangesAsync();

        var exception = await Record.ExceptionAsync(() =>
            _handler.HandleAsync(runId, CancellationToken.None));

        Assert.NotNull(exception);
        var count = await _db.DemandForecasts.CountAsync(x => x.JobRunId == runId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ExecuteAsync_RerunWithNewRunId_PreservesHistory()
    {
        var branch = new Branch("Rerun Branch", "1 Test Street", "0100000000", 10m, 106m);
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();
        _branchAId = branch.Id;

        var inventoryId = await SeedCatalogAsync(_branchAId);
        await SeedDailySalesAsync(_db, inventoryId, 7, 2);

        var firstRunId = await CreateRunAsync(_branchAId);

        await _handler.HandleAsync(firstRunId, CancellationToken.None);
        await CompleteRunAsync(firstRunId);

        var secondRunId = await CreateRunAsync(_branchAId);
        await _handler.HandleAsync(secondRunId, CancellationToken.None);

        Assert.Equal(2, await _db.DemandForecasts.CountAsync(x => x.JobRunId == firstRunId));
        Assert.Equal(2, await _db.DemandForecasts.CountAsync(x => x.JobRunId == secondRunId));
    }
}