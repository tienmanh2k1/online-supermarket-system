using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Intelligence;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests.Persistence;

public sealed class DemandForecastConfigurationTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void DemandForecast_IsMappedToDemandForecastsTable()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(DemandForecast));

        Assert.NotNull(entity);
        Assert.Equal("demand_forecasts", entity!.GetTableName());
    }

    [Fact]
    public void DemandForecast_HasRunScopedUniqueKey()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(DemandForecast))!;

        var index = entity.GetIndexes().Single(candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual([
                "JobRunId",
                "BranchInventoryId",
                "HorizonDays",
            ]));

        Assert.True(index.IsUnique);
        Assert.Equal("ix_demand_forecasts_run_inventory_horizon", index.GetDatabaseName());
    }

    [Fact]
    public void DemandForecast_RequiredColumnsAndPrecision()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(DemandForecast))!;

        Assert.False(entity.GetProperty("HorizonDays").IsNullable);
        Assert.False(entity.GetProperty("PredictedQuantity").IsNullable);
        Assert.False(entity.GetProperty("ActualDataDays").IsNullable);
        Assert.False(entity.GetProperty("AlgorithmVersion").IsNullable);
        Assert.False(entity.GetProperty("JobRunId").IsNullable);
        Assert.False(entity.GetProperty("BranchInventoryId").IsNullable);

        var predicted = entity.GetProperty("PredictedQuantity");
        Assert.Equal(18, predicted.GetPrecision());
        Assert.Equal(2, predicted.GetScale());
    }

    [Fact]
    public void DemandForecast_HasRequiredFksOnOwners()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(DemandForecast))!;

        Assert.True(entity.GetForeignKeys()
            .Single(fk => fk.Properties.Select(p => p.Name).SequenceEqual(["JobRunId"])).IsRequired);
        Assert.True(entity.GetForeignKeys()
            .Single(fk => fk.Properties.Select(p => p.Name).SequenceEqual(["BranchInventoryId"])).IsRequired);
    }
}