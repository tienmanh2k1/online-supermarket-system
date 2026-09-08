using System.Reflection;
using OnlineSupermarket.Domain.Recommendations;
using OnlineSupermarket.Infrastructure.Recommendations;
using Xunit;

namespace OnlineSupermarket.Infrastructure.Tests.Recommendations;

[Collection("MfScorerTests")]
public sealed class MfScorerTests : IDisposable
{
    private readonly DateTime _now = DateTime.UtcNow;
    private readonly Guid[] _userIds;
    private readonly ProductCandidate[] _products;
    private readonly ViewSignal[] _views;
    private readonly ScoringInput _sufficientInput;

    public MfScorerTests()
    {
        MfScorer.ClearModel();

        _userIds = Enumerable.Range(1, 5)
            .Select(i => Guid.Parse($"{i + 1000:x8}-1111-2222-3333-444444444444"))
            .ToArray();

        _products = Enumerable.Range(1, 8)
            .Select(i => new ProductCandidate(Guid.Parse($"{i + 2000:x8}-1111-2222-3333-444444444444"), Guid.NewGuid(), Guid.NewGuid(), true))
            .ToArray();

        // 30 interactions across 5 users and 8 products
        _views = _userIds.SelectMany((u, i) =>
            Enumerable.Range(0, 6).Select(j =>
                new ViewSignal(_products[(i + j) % 8].ProductId, u, null, _now))).ToArray();

        _sufficientInput = new ScoringInput(_products, _views, Array.Empty<PurchaseSignal>(), _now);
    }

    public void Dispose()
    {
        MfScorer.ClearModel();
    }

    [Fact]
    public void EnsureModel_WithSufficientData_TrainsModelAndProducesPositiveMfScores()
    {
        MfScorer.EnsureModel(_sufficientInput);

        Assert.True(MfScorer.IsModelTrained);

        var scores = _userIds.SelectMany(u => _products.Select(p => MfScorer.Score(_sufficientInput, u, p.ProductId))).ToArray();
        Assert.All(scores, s => Assert.True(s >= 0m && s <= 1m));
        Assert.Contains(scores, s => s > 0m);

        // BuildRows check
        var buildMethod = typeof(RecommendationJobHandler).GetMethod("BuildRows", BindingFlags.Static | BindingFlags.NonPublic)!;
        var rows = (IReadOnlyList<RecommendationResult>)buildMethod.Invoke(null, new object[] { _sufficientInput, Guid.NewGuid(), _now, _now.AddHours(1) })!;

        var userRows = rows.Where(r => r.Scope == RecommendationScope.User).ToList();
        Assert.NotEmpty(userRows);
        Assert.Contains(userRows, r => r.AlgorithmVersion == "mf-v1" && r.Reason == "Gợi ý từ mô hình AI" && r.Score > 0m);
    }

    [Fact]
    public void EnsureModel_WithBelowThresholdInteractions_DoesNotTrainModel_AndUsesContentFallback()
    {
        var belowThresholdInput = _sufficientInput with { Views = _views.Take(3).ToArray() };

        MfScorer.EnsureModel(belowThresholdInput);

        Assert.False(MfScorer.IsModelTrained);

        var buildMethod = typeof(RecommendationJobHandler).GetMethod("BuildRows", BindingFlags.Static | BindingFlags.NonPublic)!;
        var rows = (IReadOnlyList<RecommendationResult>)buildMethod.Invoke(null, new object[] { belowThresholdInput, Guid.NewGuid(), _now, _now.AddHours(1) })!;

        var userRows = rows.Where(r => r.Scope == RecommendationScope.User).ToList();
        Assert.DoesNotContain(userRows, r => r.AlgorithmVersion == "mf-v1");
        Assert.All(userRows, r => Assert.Equal("content-v1", r.AlgorithmVersion));
    }

    [Fact]
    public void Score_UnknownUserOrProduct_ReturnsZero()
    {
        MfScorer.EnsureModel(_sufficientInput);

        var unknownUserId = Guid.NewGuid();
        var unknownProductId = Guid.NewGuid();

        var scoreUnknownUser = MfScorer.Score(_sufficientInput, unknownUserId, _products[0].ProductId);
        var scoreUnknownProduct = MfScorer.Score(_sufficientInput, _userIds[0], unknownProductId);
        var scoreBothUnknown = MfScorer.Score(_sufficientInput, unknownUserId, unknownProductId);

        Assert.Equal(0m, scoreUnknownUser);
        Assert.Equal(0m, scoreUnknownProduct);
        Assert.Equal(0m, scoreBothUnknown);
    }

    [Fact]
    public void ScoreUserWithMf_ExcludesSeenViewsAndPurchases()
    {
        MfScorer.EnsureModel(_sufficientInput);

        var rankMethod = typeof(RecommendationJobHandler).GetMethod("ScoreUserWithMf", BindingFlags.Static | BindingFlags.NonPublic)!;

        var user0 = _userIds[0];
        var seenViewProductIds = _views.Where(v => v.UserId == user0).Select(v => v.ProductId).ToHashSet();

        // Also add a purchase for P7
        var p7 = _products[6].ProductId;
        var mixedInput = _sufficientInput with
        {
            Purchases = new[] { new PurchaseSignal(p7, user0, _now, 1) }
        };

        var (results, version, reason) = ((IReadOnlyList<ScoredProduct>, string, string))rankMethod.Invoke(null, new object[] { mixedInput, user0 })!;

        Assert.Equal("mf-v1", version);
        Assert.Equal("Gợi ý từ mô hình AI", reason);

        // Neither seen views nor purchased product should appear in MF results
        foreach (var result in results)
        {
            Assert.DoesNotContain(result.ProductId, seenViewProductIds);
            Assert.NotEqual(p7, result.ProductId);
        }
    }

    [Fact]
    public void ScoreUserWithMf_WhenAllCandidatesSeen_DoesNotReinsertSeen()
    {
        MfScorer.EnsureModel(_sufficientInput);

        var user0 = _userIds[0];
        // User has seen ALL products
        var allSeenViews = _products.Select(p => new ViewSignal(p.ProductId, user0, null, _now)).ToArray();
        var allSeenInput = _sufficientInput with { Views = allSeenViews };

        var rankMethod = typeof(RecommendationJobHandler).GetMethod("ScoreUserWithMf", BindingFlags.Static | BindingFlags.NonPublic)!;
        var (results, version, _) = ((IReadOnlyList<ScoredProduct>, string, string))rankMethod.Invoke(null, new object[] { allSeenInput, user0 })!;

        // All candidates seen must produce empty results and not re-insert seen items
        Assert.Empty(results);
    }

    [Fact]
    public void ClearModel_ResetsModelState()
    {
        MfScorer.EnsureModel(_sufficientInput);
        Assert.True(MfScorer.IsModelTrained);

        MfScorer.ClearModel();
        Assert.False(MfScorer.IsModelTrained);

        var score = MfScorer.Score(_sufficientInput, _userIds[0], _products[0].ProductId);
        Assert.Equal(0m, score);
    }
}
