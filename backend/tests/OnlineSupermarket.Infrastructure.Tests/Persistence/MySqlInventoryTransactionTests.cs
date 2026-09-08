using System.Data;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Infrastructure.Inventory;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Tests.Persistence;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MySqlInfrastructureCollection : ICollectionFixture<MySqlFixture>
{
    public const string Name = "MySql infrastructure";
}

[Collection(MySqlInfrastructureCollection.Name)]
public sealed class MySqlInventoryTransactionTests(MySqlFixture fixture) : IAsyncLifetime
{
    private readonly MySqlFixture _fixture = fixture;
    private const string TestDatabase = "online_supermarket_tests";
    private static Guid InventoryId;

    private DbContextOptions<AppDbContext> Options =>
        new DbContextOptionsBuilder<AppDbContext>().UseMySQL(_fixture.CreateDatabaseConnectionString(TestDatabase)).Options;

    public async Task InitializeAsync()
    {
        await using var master = new MySqlConnection(_fixture.MasterConnectionString);
        await master.OpenAsync();

        await using (var drop = new MySqlCommand(
            "DROP DATABASE IF EXISTS " + TestDatabase + ";", master))
        {
            await drop.ExecuteNonQueryAsync();
        }
        await using (var create = new MySqlCommand(
            "CREATE DATABASE " + TestDatabase + " CHARACTER SET utf8mb4;", master))
        {
            await create.ExecuteNonQueryAsync();
        }

        await using var db = new AppDbContext(Options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext CreateContext() => new(Options);

    private static async Task SeedInventoryRowAsync(AppDbContext db)
    {
        var category = new Category("Verified", "verified-cat");
        var brand = new Brand("Verified", "verified-brand");
        db.Categories.Add(category);
        db.Brands.Add(brand);
        await db.SaveChangesAsync();

        var branch = new Branch("Verified Branch", "1 Test Street", "09 0000 0000", 10m, 106m);
        var product = new Product(category.Id, brand.Id, "SKU-V-1", "Verified Product", "verified-product", "d", 10_000m, "cái", null);
        db.Branches.Add(branch);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        db.BranchInventories.Add(BranchInventory.Create(branch.Id, product.Id, 10_000m, 10, 5));
        await db.SaveChangesAsync();
        InventoryId = (await db.BranchInventories.SingleAsync()).Id;
    }

    private async Task<BranchInventory> LoadInventoryAsync()
    {
        await using var db = CreateContext();
        return await db.BranchInventories.AsNoTracking()
            .SingleAsync(bi => bi.Id == InventoryId);
    }

    private async Task ApplyAndCommitReserveAsync(Guid inventoryId, string operationKey)
    {
        await using var db = CreateContext();
        var service = new InventoryMutationService(db, TimeProvider.System);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await service.ApplyBatchAsync(
        [
            new InventoryMutationCommand(
                inventoryId,
                InventoryTransactionType.Reserve,
                3,
                null,
                InventoryReferenceType.Order,
                Guid.NewGuid(),
                operationKey,
                null,
                null,
                null),
        ], CancellationToken.None);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task LedgerInsertFailure_RollsBackInventoryMutation()
    {
        await using (var db = CreateContext())
        {
            await SeedInventoryRowAsync(db);
        }

        var before = await LoadInventoryAsync();
        var operationKey = $"in-batch-duplicate:{Guid.NewGuid()}";

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            ApplyBatchWithDuplicateKeysAsync(before.Id, operationKey));

        var after = await LoadInventoryAsync();
        Assert.Equal(before.QuantityOnHand, after.QuantityOnHand);
        Assert.Equal(before.ReservedQuantity, after.ReservedQuantity);
    }

    private async Task ApplyBatchWithDuplicateKeysAsync(Guid inventoryId, string operationKey)
    {
        await using var db = CreateContext();
        var service = new InventoryMutationService(db, TimeProvider.System);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await service.ApplyBatchAsync(
        [
            new InventoryMutationCommand(
                inventoryId,
                InventoryTransactionType.Reserve,
                1,
                null,
                InventoryReferenceType.Order,
                Guid.NewGuid(),
                operationKey,
                null,
                null,
                null),
            new InventoryMutationCommand(
                inventoryId,
                InventoryTransactionType.Reserve,
                2,
                null,
                InventoryReferenceType.Order,
                Guid.NewGuid(),
                operationKey,
                null,
                null,
                null),
        ], CancellationToken.None);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task ConcurrentReservations_SameOperationKey_OnlyOneApplies()
    {
        await using (var db = CreateContext())
        {
            await SeedInventoryRowAsync(db);
        }

        var operationKey = $"order:{Guid.NewGuid()}:inventory:{InventoryId}:reserve";

        var tasks = new[]
        {
            ApplyAndCommitReserveAsync(InventoryId, operationKey),
            ApplyAndCommitReserveAsync(InventoryId, operationKey),
        };

        var succeeded = 0;
        foreach (var task in tasks)
        {
            try
            {
                await task;
                succeeded++;
            }
            catch (DbUpdateException)
            {
            }
            catch (MySqlException)
            {
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("replayed with mismatched command"))
            {
            }
        }

        Assert.Equal(1, succeeded);

        var inventory = await LoadInventoryAsync();
        Assert.True(inventory.ReservedQuantity >= 0);
        Assert.True(inventory.AvailableQuantity >= 0);
        Assert.Equal(3, inventory.ReservedQuantity);

        await using var readDb = CreateContext();
        Assert.Equal(1, await readDb.InventoryTransactions.CountAsync(t => t.OperationKey == operationKey));
    }

    [Fact]
    public async Task Migrations_CreateLedgerTableWithUniqueOperationKeyIndex()
    {
        await using var db = CreateContext();

        var tableCount = await ExecuteScalarAsync(db,
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'inventory_transactions'");
        Assert.Equal(1L, tableCount);

        var indexCount = await ExecuteScalarAsync(db,
            "SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'inventory_transactions' AND INDEX_NAME = 'ix_inventory_transactions_operation_key' AND NON_UNIQUE = 0");
        Assert.Equal(1L, indexCount);
    }

    private async Task<long> ExecuteScalarAsync(AppDbContext db, string sql)
    {
        await using var connection = new MySqlConnection(_fixture.CreateDatabaseConnectionString(TestDatabase));
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }
}