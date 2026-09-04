using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Tests.Persistence;

[Collection(MySqlInfrastructureCollection.Name)]
public sealed class MySqlSchemaTests : IAsyncLifetime
{
    private const string MasterConnectionString =
        "Server=127.0.0.1;Port=3306;Database=mysql;User=root;Password=password;";
    private const string TestDatabase = "online_supermarket_schema_tests";
    private const string ConnectionString =
        "Server=127.0.0.1;Port=3306;Database=online_supermarket_schema_tests;User=root;Password=password;";

    private static DbContextOptions<AppDbContext> Options { get; } =
        new DbContextOptionsBuilder<AppDbContext>().UseMySQL(ConnectionString).Options;

    public async Task InitializeAsync()
    {
        await using var master = new MySqlConnection(MasterConnectionString);
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

    private static async Task<long> ExecuteScalarAsync(string sql)
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    [Fact]
    public async Task Migrations_CreateExactlyTwentyThreePhysicalTables()
    {
        var tableCount = await ExecuteScalarAsync(
            "SELECT COUNT(*) FROM information_schema.TABLES " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'");

        Assert.Equal(23L, tableCount);
    }

    [Fact]
    public async Task Migrations_CreateDemandForecastsTable()
    {
        var tableCount = await ExecuteScalarAsync(
            "SELECT COUNT(*) FROM information_schema.TABLES " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'demand_forecasts'");
        Assert.Equal(1L, tableCount);

        var indexCount = await ExecuteScalarAsync(
            "SELECT COUNT(*) FROM information_schema.STATISTICS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'demand_forecasts' " +
            "AND INDEX_NAME = 'ix_demand_forecasts_run_inventory_horizon' AND NON_UNIQUE = 0");
        Assert.Equal(1L, indexCount);

        var checkCount = await ExecuteScalarAsync(
            "SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'demand_forecasts' " +
            "AND CONSTRAINT_TYPE = 'CHECK' AND CONSTRAINT_NAME = 'ck_demand_forecasts_horizon'");
        Assert.Equal(1L, checkCount);
    }

    [Fact]
    public async Task Migrations_DoNotCreateDeferredStockAlertsTable()
    {
        var alertCount = await ExecuteScalarAsync(
            "SELECT COUNT(*) FROM information_schema.TABLES " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'stock_alerts'");
        Assert.Equal(0L, alertCount);

        var forecastCount = await ExecuteScalarAsync(
            "SELECT COUNT(*) FROM information_schema.TABLES " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'demand_forecasts'");
        Assert.Equal(1L, forecastCount);
    }
}