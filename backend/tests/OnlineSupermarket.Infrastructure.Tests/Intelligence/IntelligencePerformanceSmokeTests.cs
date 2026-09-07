using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Intelligence;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Tests.Intelligence;

public sealed class IntelligencePerformanceSmokeTests
{
    private static readonly TimeSpan BatchLimit = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task ForecastBatch_OneHundredBranches_CompletesUnderFiveMinutes()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            db.Database.EnsureCreated();
            var handler = new ForecastJobHandler(db, TimeProvider.System);

            await SeedOneHundredBranchesAsync(db);
            var branchIds = await db.Branches.AsNoTracking().Select(branch => branch.Id).ToListAsync();

            var startedAt = TimeProvider.System.GetUtcNow().UtcDateTime;
            foreach (var branchId in branchIds)
            {
                var run = new BackgroundJobRun("Forecast", $"branch:{branchId}", DateTime.UtcNow, branchId);
                db.BackgroundJobRuns.Add(run);
                await db.SaveChangesAsync();
                await handler.HandleAsync(run.Id, CancellationToken.None);
            }
            var elapsed = TimeProvider.System.GetUtcNow().UtcDateTime - startedAt;

            Assert.True(elapsed < BatchLimit,
                $"Forecast batch for 100 branches took {elapsed} which exceeds {BatchLimit}.");
        }
        finally
        {
            connection.Dispose();
        }
    }

    private static async Task SeedOneHundredBranchesAsync(AppDbContext db)
    {
        var category = new Category("PerfCats", "perf-cats");
        var brand = new Brand("PerfBrand", "perf-brand");
        db.Categories.Add(category);
        db.Brands.Add(brand);
        await db.SaveChangesAsync();

        for (var branchIndex = 0; branchIndex < 100; branchIndex++)
        {
            var branch = new Branch(
                $"Perf Branch {branchIndex}", "1 Test Street", "0100000000", 10m, 106m);
            db.Branches.Add(branch);

            var product = new Product(category.Id, brand.Id,
                $"SKU-P-{branchIndex:D3}",
                "Perf Product", $"perf-product-{branchIndex:D3}",
                "desc", 40_000m, "cái", null);
            db.Products.Add(product);

            var inventory = BranchInventory.Create(branch.Id, product.Id, 40_000m, 100, 5);
            db.BranchInventories.Add(inventory);

            for (var day = 0; day < 14; day++)
            {
                db.InventoryTransactions.Add(InventoryTransaction.Create(
                    inventory.Id,
                    InventoryTransactionType.Sale,
                    -2,
                    -2,
                    100 - 2,
                    0,
                    InventoryReferenceType.Order,
                    Guid.NewGuid(),
                    $"perf:sale:{Guid.NewGuid():N}",
                    null,
                    null,
                    DateTime.UtcNow.AddDays(-(day + 1))));
            }
        }

        await db.SaveChangesAsync();
    }
}