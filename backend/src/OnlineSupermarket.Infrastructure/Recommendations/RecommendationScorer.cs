namespace OnlineSupermarket.Infrastructure.Recommendations;

public static class RecommendationScorer
{
    private const int MaxGlobalResults = 20;
    private const int MaxUserResults = 20;
    private const int MaxSimilarResults = 8;
    private const int SignalWindowDays = 28;

    private const string GlobalReason = "Được nhiều người quan tâm";
    private const string UserReason = "Phù hợp danh mục đã xem";
    private const string SimilarReason = "Tương tự với sản phẩm đang xem";

    private sealed record ContributionKey(Guid OwnerId, Guid ProductId, DateTime UtcDay);

    private sealed class Popularity
    {
        public Dictionary<Guid, decimal> ViewWeight { get; } = [];
        public Dictionary<Guid, decimal> SoldWeight { get; } = [];
        public Dictionary<Guid, decimal> GlobalScore { get; } = [];
    }

    public static IReadOnlyList<ScoredProduct> ScoreGlobal(ScoringInput input)
    {
        var popularity = BuildPopularity(input);
        return Rank(
            input,
            product => popularity.GlobalScore[product.ProductId],
            GlobalReason,
            MaxGlobalResults);
    }

    public static IReadOnlyList<ScoredProduct> ScoreUser(ScoringInput input, Guid userId)
    {
        var popularity = BuildPopularity(input);
        var contributions = ContributionPerProduct(input, userId);
        var categoryAffinity = new Dictionary<Guid, decimal>();
        var brandAffinity = new Dictionary<Guid, decimal>();

        foreach (var (productId, weight) in contributions)
        {
            var product = FindProduct(input, productId);
            if (product.ProductId == Guid.Empty)
            {
                continue;
            }

            categoryAffinity[product.CategoryId] = GetOrDefault(categoryAffinity, product.CategoryId) + weight;
            brandAffinity[product.BrandId] = GetOrDefault(brandAffinity, product.BrandId) + weight;
        }

        var maxCategory = categoryAffinity.Count > 0 ? categoryAffinity.Values.Max() : 0m;
        var maxBrand = brandAffinity.Count > 0 ? brandAffinity.Values.Max() : 0m;

        return Rank(
            input,
            product =>
            {
                var categoryNormalized = Normalize(GetOrDefault(categoryAffinity, product.CategoryId), maxCategory);
                var brandNormalized = Normalize(GetOrDefault(brandAffinity, product.BrandId), maxBrand);
                return clamp01(
                    0.55m * categoryNormalized
                    + 0.25m * brandNormalized
                    + 0.20m * popularity.GlobalScore[product.ProductId]);
            },
            UserReason,
            MaxUserResults,
            excluded: product => contributions.ContainsKey(product.ProductId));
    }

    public static IReadOnlyList<ScoredProduct> ScoreSimilarProducts(ScoringInput input, Guid sourceProductId)
    {
        var popularity = BuildPopularity(input);
        var source = FindProduct(input, sourceProductId);

        return Rank(
            input,
            product =>
            {
                if (source.ProductId == Guid.Empty)
                {
                    return 0m;
                }

                var categoryMatch = product.CategoryId == source.CategoryId ? 0.60m : 0m;
                var brandMatch = product.BrandId == source.BrandId ? 0.30m : 0m;
                return clamp01(categoryMatch + brandMatch + 0.10m * popularity.GlobalScore[product.ProductId]);
            },
            SimilarReason,
            MaxSimilarResults,
            excluded: product => product.ProductId == sourceProductId);
    }

    private static Popularity BuildPopularity(ScoringInput input)
    {
        var popularity = new Popularity();
        var contributed = new HashSet<ContributionKey>();

        foreach (var view in input.Views)
        {
            if (AgeDays(input.NowUtc, view.ViewedAtUtc) >= SignalWindowDays)
            {
                continue;
            }

            var ownerId = view.AnonymousSessionId ?? view.UserId;
            if (!ownerId.HasValue)
            {
                continue;
            }

            if (!contributed.Add(new ContributionKey(ownerId.Value, view.ProductId, view.ViewedAtUtc.Date)))
            {
                continue;
            }

            popularity.ViewWeight[view.ProductId] =
                GetOrDefault(popularity.ViewWeight, view.ProductId) + ViewWeight(view.ViewedAtUtc, input.NowUtc);
        }

        foreach (var purchase in input.Purchases)
        {
            if (AgeDays(input.NowUtc, purchase.CompletedAtUtc) >= SignalWindowDays)
            {
                continue;
            }

            popularity.SoldWeight[purchase.ProductId] =
                GetOrDefault(popularity.SoldWeight, purchase.ProductId)
                + purchase.Quantity * 3m * ViewWeight(purchase.CompletedAtUtc, input.NowUtc);
        }

        var maxViews = popularity.ViewWeight.Count > 0 ? popularity.ViewWeight.Values.Max() : 0m;
        var maxSold = popularity.SoldWeight.Count > 0 ? popularity.SoldWeight.Values.Max() : 0m;

        foreach (var product in input.Products)
        {
            var views = Normalize(GetOrDefault(popularity.ViewWeight, product.ProductId), maxViews);
            var sold = Normalize(GetOrDefault(popularity.SoldWeight, product.ProductId), maxSold);
            popularity.GlobalScore[product.ProductId] = clamp01(0.40m * views + 0.60m * sold);
        }

        return popularity;
    }

    private static Dictionary<Guid, decimal> ContributionPerProduct(ScoringInput input, Guid userId)
    {
        var contributions = new Dictionary<Guid, decimal>();
        var contributed = new HashSet<ContributionKey>();

        foreach (var view in input.Views)
        {
            if (view.UserId != userId || AgeDays(input.NowUtc, view.ViewedAtUtc) >= SignalWindowDays)
            {
                continue;
            }

            if (!contributed.Add(new ContributionKey(userId, view.ProductId, view.ViewedAtUtc.Date)))
            {
                continue;
            }

            contributions[view.ProductId] =
                GetOrDefault(contributions, view.ProductId) + ViewWeight(view.ViewedAtUtc, input.NowUtc);
        }

        foreach (var purchase in input.Purchases)
        {
            if (purchase.UserId != userId || AgeDays(input.NowUtc, purchase.CompletedAtUtc) >= SignalWindowDays)
            {
                continue;
            }

            contributions[purchase.ProductId] =
                GetOrDefault(contributions, purchase.ProductId)
                + purchase.Quantity * 3m * ViewWeight(purchase.CompletedAtUtc, input.NowUtc);
        }

        return contributions;
    }

    private static IReadOnlyList<ScoredProduct> Rank(
        ScoringInput input,
        Func<ProductCandidate, decimal> scoreFor,
        string reason,
        int limit,
        Func<ProductCandidate, bool>? excluded = null)
    {
        var candidates = input.Products
            .Where(product => product.IsActive && (excluded == null || !excluded(product)))
            .Select(product => new ScoredProduct(product.ProductId, scoreFor(product), reason))
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.ProductId);

        return candidates.Take(limit).ToArray();
    }

    private static ProductCandidate FindProduct(ScoringInput input, Guid productId)
        => input.Products.FirstOrDefault(product => product.ProductId == productId);

    private static decimal ViewWeight(DateTime atUtc, DateTime nowUtc)
    {
        var ageDays = (decimal)AgeDays(nowUtc, atUtc);
        return 1m / (1m + ageDays);
    }

    private static int AgeDays(DateTime nowUtc, DateTime atUtc)
    {
        var days = (nowUtc.Date - atUtc.Date).Days;
        return days < 0 ? 0 : days;
    }

    private static decimal Normalize(decimal value, decimal max)
        => max <= 0 ? 0m : value / max;

    private static decimal clamp01(decimal value)
        => value < 0m ? 0m : value > 1m ? 1m : value;

    private static decimal GetOrDefault(Dictionary<Guid, decimal> dictionary, Guid key)
    {
        var value = dictionary.TryGetValue(key, out var stored) ? stored : 0m;
        return value;
    }
}