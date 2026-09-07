using OnlineSupermarket.Infrastructure.Recommendations;

namespace OnlineSupermarket.Infrastructure.Tests.Recommendations;

public sealed class RecommendationScorerTests
{
    private static readonly DateTime Now = new(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _categoryA = Guid.NewGuid();
    private readonly Guid _categoryB = Guid.NewGuid();
    private readonly Guid _brandA = Guid.NewGuid();
    private readonly Guid _brandB = Guid.NewGuid();
    private readonly Guid _sourceProductId = Guid.NewGuid();
    private readonly Guid _inactiveProductId = Guid.NewGuid();

    private sealed class InputBuilder
    {
        public readonly List<ProductCandidate> Products = [];
        public readonly List<ViewSignal> Views = [];
        public readonly List<PurchaseSignal> Purchases = [];
        public DateTime NowUtc = Now;

        public Guid AddProduct(Guid productId, Guid categoryId, Guid brandId, bool isActive = true)
        {
            Products.Add(new ProductCandidate(productId, categoryId, brandId, isActive));
            return productId;
        }

        public void AddView(Guid productId, Guid userId, DateTime viewedAtUtc)
            => Views.Add(new ViewSignal(productId, userId, null, viewedAtUtc));

        public void AddPurchase(Guid productId, Guid userId, DateTime completedAtUtc, int quantity)
            => Purchases.Add(new PurchaseSignal(productId, userId, completedAtUtc, quantity));

        public ScoringInput Build() => new(Products, Views, Purchases, NowUtc);
    }

    private static DateTime Day(int daysAgo)
        => new DateTime(Now.Date.Year, Now.Date.Month, Now.Date.Day, 12, 0, 0, DateTimeKind.Utc)
            .AddDays(-daysAgo);

    [Fact]
    public void ScoreGlobal_RanksPurchasesAboveViewsAndDecaysRecency()
    {
        var builder = new InputBuilder();
        var productA = builder.AddProduct(Guid.NewGuid(), _categoryA, _brandA);
        var productB = builder.AddProduct(Guid.NewGuid(), _categoryA, _brandA);

        builder.AddView(productA, Guid.NewGuid(), Day(0));
        builder.AddView(productA, Guid.NewGuid(), Day(1));
        builder.AddPurchase(productA, Guid.NewGuid(), Day(0), 1);
        builder.AddView(productB, Guid.NewGuid(), Day(10));

        var ranked = RecommendationScorer.ScoreGlobal(builder.Build());

        Assert.True(ranked.Count >= 2);
        Assert.Equal(productA, ranked[0].ProductId);
        Assert.Equal(productB, ranked[1].ProductId);
        Assert.True(ranked[0].Score > ranked[1].Score);
    }

    [Fact]
    public void ScoreUser_CapsRepeatedViewsPerProductPerUtcDay()
    {
        var viewed = Guid.NewGuid();
        var candidate = Guid.NewGuid();
        var otherCategory = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var rankedOne = ScoreUserViewsAsync(viewed, candidate, otherCategory, userId, viewCount: 1);
        var rankedFifty = ScoreUserViewsAsync(viewed, candidate, otherCategory, userId, viewCount: 50);

        Assert.Equal(rankedOne.Select(x => x.Score), rankedFifty.Select(x => x.Score));
    }

    private List<ScoredProduct> ScoreUserViewsAsync(
        Guid viewed,
        Guid candidate,
        Guid otherCategory,
        Guid userId,
        int viewCount)
    {
        var builder = new InputBuilder();
        builder.AddProduct(viewed, _categoryA, _brandA);
        builder.AddProduct(candidate, _categoryA, _brandA);
        builder.AddProduct(otherCategory, _categoryB, _brandB);

        for (var index = 0; index < viewCount; index++)
        {
            builder.AddView(viewed, userId, Day(0));
        }

        return RecommendationScorer.ScoreUser(builder.Build(), userId).ToList();
    }

    [Fact]
    public void ScoreSimilar_ExcludesSourceAndInactiveProducts()
    {
        var builder = new InputBuilder();
        var sameCategory = builder.AddProduct(Guid.NewGuid(), _categoryA, _brandA);
        var differentBrand = builder.AddProduct(Guid.NewGuid(), _categoryA, _brandB);
        var otherCategory = builder.AddProduct(Guid.NewGuid(), _categoryB, _brandA);
        builder.AddProduct(_sourceProductId, _categoryA, _brandA);
        builder.AddProduct(_inactiveProductId, _categoryA, _brandA, isActive: false);

        builder.AddView(sameCategory, Guid.NewGuid(), Day(0));

        var ranked = RecommendationScorer.ScoreSimilarProducts(builder.Build(), _sourceProductId);

        Assert.DoesNotContain(ranked, x => x.ProductId == _sourceProductId);
        Assert.DoesNotContain(ranked, x => x.ProductId == _inactiveProductId);
        Assert.True(ranked.SequenceEqual(ranked
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.ProductId)
            .ToArray()));
    }

[Fact]
    public void ScoreSimilar_TieBreaksByProductIdAscending()
    {
        var builder = new InputBuilder();
        var first = builder.AddProduct(Guid.NewGuid(), _categoryA, _brandA);
        var second = builder.AddProduct(Guid.NewGuid(), _categoryA, _brandA);
        builder.AddProduct(_sourceProductId, _categoryA, _brandA);

        var ranked = RecommendationScorer.ScoreSimilarProducts(builder.Build(), _sourceProductId);

        Assert.Equal(2, ranked.Count);
        Assert.Equal((first.ToString().CompareTo(second.ToString()) < 0 ? first : second), ranked[0].ProductId);
        Assert.Equal((first.ToString().CompareTo(second.ToString()) < 0 ? second : first), ranked[1].ProductId);
    }

    [Fact]
    public void ScoreUser_ExcludesAlreadySeenProducts()
    {
        var builder = new InputBuilder();
        var seen = builder.AddProduct(Guid.NewGuid(), _categoryA, _brandA);
        builder.AddProduct(Guid.NewGuid(), _categoryA, _brandA);
        var userId = Guid.NewGuid();
        builder.AddView(seen, userId, Day(0));

        var ranked = RecommendationScorer.ScoreUser(builder.Build(), userId);

        Assert.DoesNotContain(ranked, x => x.ProductId == seen);
    }

    [Fact]
    public void ScoreUser_WeightsPurchasesThreeTimesViews()
    {
        var builder = new InputBuilder();
        var purchased = builder.AddProduct(Guid.NewGuid(), _categoryA, _brandA);
        var viewed = builder.AddProduct(Guid.NewGuid(), _categoryB, _brandB);
        var purchasedCategoryCandidate = builder.AddProduct(Guid.NewGuid(), _categoryA, _brandA);
        var viewedCategoryCandidate = builder.AddProduct(Guid.NewGuid(), _categoryB, _brandB);
        var userId = Guid.NewGuid();
        builder.AddPurchase(purchased, userId, Day(0), 1);
        builder.AddView(viewed, userId, Day(0));

        var ranked = RecommendationScorer.ScoreUser(builder.Build(), userId);

        var ids = ranked.Select(x => x.ProductId).ToList();
        var purchasedPos = ids.IndexOf(purchasedCategoryCandidate);
        var viewedPos = ids.IndexOf(viewedCategoryCandidate);
        Assert.True(purchasedPos >= 0 && viewedPos >= 0);
        Assert.True(purchasedPos < viewedPos);
    }

    [Fact]
    public void ScoreGlobal_SkipsSignalsOutsideWindow()
    {
        var builder = new InputBuilder();
        var stale = builder.AddProduct(Guid.NewGuid(), _categoryA, _brandA);
        builder.AddView(stale, Guid.NewGuid(), Day(30));

        var ranked = RecommendationScorer.ScoreGlobal(builder.Build());

        Assert.Empty(ranked);
    }

    [Fact]
    public void ScoreGlobal_LimitsToTwentyResults()
    {
        var builder = new InputBuilder();
        for (var index = 0; index < 25; index++)
        {
            var product = builder.AddProduct(Guid.NewGuid(), _categoryA, _brandA);
            builder.AddView(product, Guid.NewGuid(), Day(0));
        }

        var ranked = RecommendationScorer.ScoreGlobal(builder.Build());

        Assert.True(ranked.Count <= 20);
    }

    [Fact]
    public void ScoreSimilar_LimitsToEightResults()
    {
        var builder = new InputBuilder();
        builder.AddProduct(_sourceProductId, _categoryA, _brandA);
        for (var index = 0; index < 10; index++)
        {
            builder.AddProduct(Guid.NewGuid(), _categoryA, _brandA);
        }

        var ranked = RecommendationScorer.ScoreSimilarProducts(builder.Build(), _sourceProductId);

        Assert.True(ranked.Count <= 8);
    }

    [Fact]
    public void ScoreGlobal_ReturnsCustomerSafeReasonsAndValidScores()
    {
        var builder = new InputBuilder();
        var product = builder.AddProduct(Guid.NewGuid(), _categoryA, _brandA);
        builder.AddView(product, Guid.NewGuid(), Day(0));
        builder.AddPurchase(product, Guid.NewGuid(), Day(0), 1);

        var ranked = RecommendationScorer.ScoreGlobal(builder.Build());

        Assert.Single(ranked);
        Assert.False(string.IsNullOrWhiteSpace(ranked[0].Reason));
        Assert.True(ranked[0].Reason.Length <= 500);
        Assert.True(ranked[0].Score >= 0m && ranked[0].Score <= 1m);
    }
}