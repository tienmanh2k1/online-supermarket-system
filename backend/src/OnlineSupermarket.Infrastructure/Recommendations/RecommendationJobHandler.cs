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
    private const string AlgorithmVersion = "content-v1";

    public string JobName => "Recommendations";

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
        var rows = BuildRows(input, runId, nowUtc, expiresAtUtc);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
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
                AlgorithmVersion, generatedAtUtc, expiresAtUtc, runId));
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
            var ranked = RecommendationScorer.ScoreUser(input, userId);
            for (var index = 0; index < ranked.Count; index++)
            {
                var scored = ranked[index];
                rows.Add(RecommendationResult.CreateForUser(
                    userId, scored.ProductId, scored.Score, index + 1, scored.Reason,
                    AlgorithmVersion, generatedAtUtc, expiresAtUtc, runId));
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
                    AlgorithmVersion, generatedAtUtc, expiresAtUtc, runId));
            }
        }

        return rows;
    }
}