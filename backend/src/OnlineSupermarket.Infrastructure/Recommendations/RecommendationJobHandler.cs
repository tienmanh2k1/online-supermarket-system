using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Recommendations;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Recommendations;

public class RecommendationJobHandler(
    AppDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<IntelligenceJobsOptions> options) : IBackgroundJobHandler
{
    private const int SignalWindowDays = 28;
    private const int ExpiryGraceMinutes = 15;
    private const string AlgorithmVersionMf = "mf-v1";
    private const string AlgorithmVersionContent = "content-v1";

    public string JobName => "Recommendations";

    internal Action<ScoringInput>? EnsureModelSeam { get; set; }

    public async Task HandleAsync(Guid runId, CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var windowStart = nowUtc.AddDays(-SignalWindowDays);

        var products = await dbContext.Products.AsNoTracking()
            .Where(product => product.IsActive)
            .Select(product => new ProductCandidate(product.Id, product.CategoryId, product.BrandId, true))
            .ToListAsync(cancellationToken);

        var viewsProjection = await dbContext.ProductViewEvents.AsNoTracking()
            .Where(view => view.ViewedAtUtc >= windowStart)
            .Select(view => new ViewSignal(view.ProductId, view.UserId, view.AnonymousSessionId, view.ViewedAtUtc))
            .ToListAsync(cancellationToken);

        var purchasesProjection = await (from orderItem in dbContext.OrderItems.AsNoTracking()
            join order in dbContext.Orders.AsNoTracking() on orderItem.OrderId equals order.Id
            where order.Status == OrderStatus.Completed && order.UpdatedAtUtc >= windowStart
            select new { orderItem.ProductId, order.UserId, order.UpdatedAtUtc, orderItem.Quantity })
            .ToListAsync(cancellationToken);

        var purchases = purchasesProjection
            .Select(item => new PurchaseSignal(item.ProductId, item.UserId, item.UpdatedAtUtc, item.Quantity))
            .ToArray();

        var intervalMinutes = Math.Max(1, options.Value.RecommendationIntervalMinutes);
        var expiresAtUtc = nowUtc.AddMinutes(intervalMinutes + ExpiryGraceMinutes);

        var input = new ScoringInput(products, viewsProjection, purchases, nowUtc);

        try
        {
            if (EnsureModelSeam != null)
            {
                EnsureModelSeam(input);
            }
            else
            {
                MfScorer.EnsureModel(input);
            }

            var rows = BuildRows(input, runId, nowUtc, expiresAtUtc);

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await JobRunPublishGuard.EnsureOwnedAsync(dbContext, runId, timeProvider, cancellationToken);
                dbContext.RecommendationResults.AddRange(rows);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch
        {
            MfScorer.ClearModel();
            throw;
        }
    }

    private static IReadOnlyList<RecommendationResult> BuildRows(
        ScoringInput input,
        Guid runId,
        DateTime generatedAtUtc,
        DateTime expiresAtUtc)
    {
        var rows = new List<RecommendationResult>();
        var activeProductIds = input.Products.Select(product => product.ProductId).ToHashSet();

        var global = RecommendationScorer.ScoreGlobal(input);
        for (var index = 0; index < global.Count; index++)
        {
            var scored = global[index];
            rows.Add(RecommendationResult.CreateGlobal(
                scored.ProductId, scored.Score, index + 1, scored.Reason,
                AlgorithmVersionContent, generatedAtUtc, expiresAtUtc, runId));
        }

        var userIds = new HashSet<Guid>();
        foreach (var view in input.Views)
        {
            if (view.UserId.HasValue)
            {
                userIds.Add(view.UserId.Value);
            }
        }
        foreach (var purchase in input.Purchases)
        {
            if (purchase.UserId.HasValue)
            {
                userIds.Add(purchase.UserId.Value);
            }
        }

        foreach (var userId in userIds)
        {
            var (ranked, version, reason) = ScoreUserWithMf(input, userId);
            for (var index = 0; index < ranked.Count; index++)
            {
                var scored = ranked[index];
                rows.Add(RecommendationResult.CreateForUser(
                    userId, scored.ProductId, scored.Score, index + 1, reason,
                    version, generatedAtUtc, expiresAtUtc, runId));
            }
        }

        var sourceProductIds = input.Views
            .Select(view => view.ProductId)
            .Where(activeProductIds.Contains)
            .ToHashSet();

        foreach (var sourceProductId in sourceProductIds)
        {
            var ranked = RecommendationScorer.ScoreSimilarProducts(input, sourceProductId);
            for (var index = 0; index < ranked.Count; index++)
            {
                var scored = ranked[index];
                rows.Add(RecommendationResult.CreateSimilarProduct(
                    sourceProductId, scored.ProductId, scored.Score, index + 1, scored.Reason,
                    AlgorithmVersionContent, generatedAtUtc, expiresAtUtc, runId));
            }
        }

        return rows;
    }

    private static (IReadOnlyList<ScoredProduct> Results, string Version, string Reason) ScoreUserWithMf(ScoringInput input, Guid userId)
    {
        const int maxResults = 20;
        const string mfReason = "Gợi ý từ mô hình AI";
        const string contentReason = "Phù hợp danh mục đã xem";

        if (!MfScorer.IsModelTrained)
        {
            var fallback = RecommendationScorer.ScoreUser(input, userId);
            return (fallback, AlgorithmVersionContent, contentReason);
        }

        var seenProducts = GetSeenProducts(input, userId);

        var candidates = input.Products
            .Where(p => p.IsActive && !seenProducts.Contains(p.ProductId))
            .Select(p => new
            {
                Product = p,
                MfScore = MfScorer.Score(input, userId, p.ProductId)
            })
            .Where(x => x.MfScore > 0)
            .OrderByDescending(x => x.MfScore)
            .Take(maxResults)
            .Select(x => new ScoredProduct(x.Product.ProductId, x.MfScore, mfReason))
            .ToList();

        if (candidates.Count == 0)
        {
            var fallback = RecommendationScorer.ScoreUser(input, userId);
            return (fallback, AlgorithmVersionContent, contentReason);
        }

        return (candidates, AlgorithmVersionMf, mfReason);
    }

    private static HashSet<Guid> GetSeenProducts(ScoringInput input, Guid userId)
    {
        var seen = new HashSet<Guid>();

        foreach (var view in input.Views)
        {
            if (view.UserId == userId)
            {
                seen.Add(view.ProductId);
            }
        }

        foreach (var purchase in input.Purchases)
        {
            if (purchase.UserId == userId)
            {
                seen.Add(purchase.ProductId);
            }
        }

        return seen;
    }
}