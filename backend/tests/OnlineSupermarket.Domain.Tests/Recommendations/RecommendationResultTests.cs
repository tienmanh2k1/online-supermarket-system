using OnlineSupermarket.Domain.Recommendations;

namespace OnlineSupermarket.Domain.Tests.Recommendations;

public sealed class RecommendationResultTests
{
    private static DateTime Now => new(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GlobalResult_StableAudienceAndNoOwners()
    {
        var result = RecommendationResult.CreateGlobal(
            Guid.NewGuid(), 0.75m, 1, "Được nhiều khách quan tâm",
            "content-v1", Now, Now.AddHours(2), Guid.NewGuid());

        Assert.Equal(RecommendationScope.Global, result.Scope);
        Assert.Equal("global", result.AudienceKey);
        Assert.Null(result.UserId);
        Assert.Null(result.SourceProductId);
    }

    [Fact]
    public void UserResult_BuildsStableAudienceKey()
    {
        var userId = Guid.NewGuid();
        var result = RecommendationResult.CreateForUser(
            userId, Guid.NewGuid(), 0.75m, 1, "Phù hợp danh mục đã xem",
            "content-v1", Now, Now.AddHours(2), Guid.NewGuid());

        Assert.Equal(RecommendationScope.User, result.Scope);
        Assert.Equal($"user:{userId}", result.AudienceKey);
        Assert.Equal(userId, result.UserId);
        Assert.Null(result.SourceProductId);
    }

    [Fact]
    public void SimilarProductResult_BuildsStableAudienceKey()
    {
        var sourceProductId = Guid.NewGuid();
        var result = RecommendationResult.CreateSimilarProduct(
            sourceProductId, Guid.NewGuid(), 0.5m, 1, "Sản tương tự đã xem",
            "content-v1", Now, Now.AddHours(2), Guid.NewGuid());

        Assert.Equal(RecommendationScope.SimilarProduct, result.Scope);
        Assert.Equal($"product:{sourceProductId}", result.AudienceKey);
        Assert.Equal(sourceProductId, result.SourceProductId);
        Assert.Null(result.UserId);
    }

    [Fact]
    public void Create_WithNonPositiveRank_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecommendationResult.CreateGlobal(
                Guid.NewGuid(), 0.5m, 0, "reason", "content-v1", Now, Now.AddHours(2), Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithScoreBelowZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecommendationResult.CreateGlobal(
                Guid.NewGuid(), -0.01m, 1, "reason", "content-v1", Now, Now.AddHours(2), Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithScoreAboveOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecommendationResult.CreateGlobal(
                Guid.NewGuid(), 1.01m, 1, "reason", "content-v1", Now, Now.AddHours(2), Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithExpiryNotAfterGeneration_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            RecommendationResult.CreateGlobal(
                Guid.NewGuid(), 0.5m, 1, "reason", "content-v1", Now, Now, Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithNonUtcTimestamps_Throws()
    {
        var local = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() =>
            RecommendationResult.CreateGlobal(
                Guid.NewGuid(), 0.5m, 1, "reason", "content-v1", Now, local, Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithEmptyRecommendedProduct_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            RecommendationResult.CreateGlobal(
                Guid.Empty, 0.5m, 1, "reason", "content-v1", Now, Now.AddHours(2), Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithEmptyJobRunId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            RecommendationResult.CreateGlobal(
                Guid.NewGuid(), 0.5m, 1, "reason", "content-v1", Now, Now.AddHours(2), Guid.Empty));
    }

    [Fact]
    public void Create_WithEmptyReason_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            RecommendationResult.CreateGlobal(
                Guid.NewGuid(), 0.5m, 1, "  ", "content-v1", Now, Now.AddHours(2), Guid.NewGuid()));
    }

    [Fact]
    public void UserResult_WithEmptyUserId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            RecommendationResult.CreateForUser(
                Guid.Empty, Guid.NewGuid(), 0.5m, 1, "reason", "content-v1",
                Now, Now.AddHours(2), Guid.NewGuid()));
    }

    [Fact]
    public void SimilarProductResult_WithEmptySourceProduct_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            RecommendationResult.CreateSimilarProduct(
                Guid.Empty, Guid.NewGuid(), 0.5m, 1, "reason", "content-v1",
                Now, Now.AddHours(2), Guid.NewGuid()));
    }
}