using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Recommendations;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests.Persistence;

public sealed class RecommendationResultConfigurationTests
{
    [Fact]
    public void RecommendationResult_IsMappedToRecommendationResultsTable()
    {
        using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(RecommendationResult));

        Assert.NotNull(entity);
        Assert.Equal("recommendation_results", entity!.GetTableName());
    }

    [Fact]
    public void RecommendationResult_HasAudienceScopedUniqueIndex()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(RecommendationResult))!;

        var index = entity.GetIndexes().Single(candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual([
                "JobRunId",
                "AudienceKey",
                "RecommendedProductId",
            ]));

        Assert.True(index.IsUnique);
        Assert.Equal("ix_recommendation_results_run_audience_product", index.GetDatabaseName());
    }

    [Fact]
    public void RecommendationResult_AudienceKeyIsRequired()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(RecommendationResult))!;

        Assert.False(entity.GetProperty("AudienceKey").IsNullable);
    }

    [Fact]
    public void RecommendationResult_HasRequiredFksOnOwners()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(RecommendationResult))!;

        Assert.True(entity.GetForeignKeys()
            .Single(fk => fk.Properties.Select(p => p.Name).SequenceEqual(["JobRunId"])).IsRequired);
        Assert.True(entity.GetForeignKeys()
            .Single(fk => fk.Properties.Select(p => p.Name).SequenceEqual(["RecommendedProductId"])).IsRequired);
        Assert.False(entity.GetForeignKeys()
            .Single(fk => fk.Properties.Select(p => p.Name).SequenceEqual(["UserId"])).IsRequired);
        Assert.False(entity.GetForeignKeys()
            .Single(fk => fk.Properties.Select(p => p.Name).SequenceEqual(["SourceProductId"])).IsRequired);
    }

    [Fact]
    public void RecommendationResult_ScoreHasPrecisionAndRankRequired()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(RecommendationResult))!;

        var score = entity.GetProperty("Score");
        Assert.Equal(12, score.GetPrecision());
        Assert.Equal(6, score.GetScale());
        Assert.False(entity.GetProperty("Rank").IsNullable);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}