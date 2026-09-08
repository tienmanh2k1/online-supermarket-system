using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Tests.Persistence;

[Collection(MySqlInfrastructureCollection.Name)]
public sealed class MySqlSchemaTests(MySqlFixture fixture) : IAsyncLifetime
{
    private readonly MySqlFixture _fixture = fixture;
    private const string TestDatabase = "online_supermarket_schema_tests";

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

    private async Task<long> ExecuteScalarAsync(string sql)
    {
        await using var connection = new MySqlConnection(_fixture.CreateDatabaseConnectionString(TestDatabase));
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    [Fact]
    public async Task Migrations_EnforceRecommendationBounds()
    {
        var checkCount = await ExecuteScalarAsync(
            "SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'recommendation_results' " +
            "AND CONSTRAINT_TYPE = 'CHECK' AND CONSTRAINT_NAME IN " +
            "('ck_recommendation_results_rank', 'ck_recommendation_results_score')");
        Assert.Equal(2L, checkCount);
    }

    [Fact]
    public async Task Migrations_MatchCurrentModelAndCanBeAppliedAgain()
    {
        await using var db = new AppDbContext(Options);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await db.Database.MigrateAsync();
    }

    [Fact]
    public async Task Migrations_CreateExactlyTwentyThreePhysicalTables()
    {
        var tableCount = await ExecuteScalarAsync(
            "SELECT COUNT(*) FROM information_schema.TABLES " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE' " +
            "AND TABLE_NAME <> '__EFMigrationsHistory'");

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
        Assert.Equal(3L, indexCount);
        var matchingColumns = await ExecuteScalarAsync(
            "SELECT COUNT(*) FROM information_schema.STATISTICS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'demand_forecasts' " +
            "AND INDEX_NAME = 'ix_demand_forecasts_run_inventory_horizon' AND NON_UNIQUE = 0 " +
            "AND ((SEQ_IN_INDEX = 1 AND COLUMN_NAME = 'job_run_id') " +
            "OR (SEQ_IN_INDEX = 2 AND COLUMN_NAME = 'branch_inventory_id') " +
            "OR (SEQ_IN_INDEX = 3 AND COLUMN_NAME = 'horizon_days'))");
        Assert.Equal(3L, matchingColumns);

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
