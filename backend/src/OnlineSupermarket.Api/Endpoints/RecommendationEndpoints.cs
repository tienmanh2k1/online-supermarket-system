using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Api.Contracts.Recommendation;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Domain.Recommendations;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;
using OnlineSupermarket.Infrastructure.Recommendations;

namespace OnlineSupermarket.Api.Endpoints;

public static class RecommendationEndpoints
{
    public static IEndpointRouteBuilder MapRecommendationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Recommendations");

        group.MapPost("/products/{productId:guid}/view-events", RecordProductViewAsync)
            .WithName("RecordProductViewEvent")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/recommendations/session/merge", MergeSessionAsync)
            .WithName("MergeRecommendationSession")
            .RequireAuthorization()
            .Produces<MergeSessionResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/recommendations", GetRecommendationsAsync)
            .WithName("GetRecommendations")
            .Produces<RecommendationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/products/{productId:guid}/recommendations", GetProductRecommendationsAsync)
            .WithName("GetProductRecommendations")
            .Produces<RecommendationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        var adminGroup = routes.MapGroup("/api/admin")
            .WithTags("Admin Recommendations")
            .RequireAuthorization("AdminOnly");

        adminGroup.MapGet("/recommendations/results", GetAdminRecommendationResultsAsync)
            .WithName("GetAdminRecommendationResults")
            .Produces<RecommendationSampleResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        adminGroup.MapPost("/jobs/recommendations/runs", TriggerRecommendationRunAsync)
            .WithName("TriggerRecommendationRun")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return routes;
    }

    private static async Task<IResult> RecordProductViewAsync(
        [FromRoute] Guid productId,
        [FromBody] RecordProductViewRequest request,
        [FromServices] AppDbContext dbContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (request.AnonymousSessionId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Anonymous session id is required." });
        }

        var productExists = await dbContext.Products
            .AnyAsync(p => p.Id == productId, cancellationToken);

        if (!productExists)
        {
            return Results.NotFound(new { message = "Product not found." });
        }

        var userId = TryGetUserId(httpContext.User);
        var view = userId.HasValue
            ? ProductViewEvent.Create(productId, userId, null, request.BranchId, DateTime.UtcNow)
            : ProductViewEvent.Create(productId, null, request.AnonymousSessionId, request.BranchId, DateTime.UtcNow);

        dbContext.ProductViewEvents.Add(view);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Accepted();
    }

    private static Guid? TryGetUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var userId) ? userId : null;
    }

    private static async Task<IResult> MergeSessionAsync(
        [FromBody] MergeSessionRequest request,
        [FromServices] IProductViewEventStore viewEventStore,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (request.AnonymousSessionId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Anonymous session id is required." });
        }

        var userId = TryGetUserId(user);
        if (!userId.HasValue)
        {
            return Results.Unauthorized();
        }

        var mergedCount = await viewEventStore.MergeAnonymousSessionAsync(
            request.AnonymousSessionId, userId.Value, cancellationToken);

        return Results.Ok(new MergeSessionResponse(mergedCount));
    }

    private static async Task<IResult> GetRecommendationsAsync(
        [FromQuery] Guid? branchId,
        [FromQuery] int limit = 8,
        HttpContext httpContext = null!,
        [FromServices] AppDbContext dbContext = null!,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1 || limit > 20)
        {
            return Results.BadRequest(new { message = "Limit must be between 1 and 20." });
        }

        var userId = TryGetUserId(httpContext.User);
        var jobRunId = await LatestSucceededRunIdAsync(dbContext, cancellationToken);
        if (jobRunId == null)
        {
            return Results.Ok(new RecommendationResponse(null, null, []));
        }

        var nowUtc = DateTime.UtcNow;

        if (userId.HasValue)
        {
            var userScope = await LoadScopeItemsAsync(
                dbContext, jobRunId.Value, RecommendationScope.User,
                $"user:{userId.Value}", branchId, limit, nowUtc, cancellationToken);
            if (userScope.Items.Count > 0)
            {
                return Results.Ok(new RecommendationResponse(
                    nameof(RecommendationScope.User), userScope.GeneratedAtUtc, userScope.Items));
            }
        }

        var global = await LoadScopeItemsAsync(
            dbContext, jobRunId.Value, RecommendationScope.Global,
            "global", branchId, limit, nowUtc, cancellationToken);

        if (global.Items.Count == 0)
        {
            return Results.Ok(new RecommendationResponse(null, null, []));
        }

        return Results.Ok(new RecommendationResponse(
            nameof(RecommendationScope.Global), global.GeneratedAtUtc, global.Items));
    }

    private static async Task<IResult> GetProductRecommendationsAsync(
        [FromRoute] Guid productId,
        [FromQuery] Guid? branchId,
        [FromQuery] int limit = 8,
        [FromServices] AppDbContext dbContext = null!,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1 || limit > 20)
        {
            return Results.BadRequest(new { message = "Limit must be between 1 and 20." });
        }

        var jobRunId = await LatestSucceededRunIdAsync(dbContext, cancellationToken);
        if (jobRunId == null)
        {
            return Results.Ok(new RecommendationResponse(null, null, []));
        }

        var nowUtc = DateTime.UtcNow;
        var similar = await LoadScopeItemsAsync(
            dbContext, jobRunId.Value, RecommendationScope.SimilarProduct,
            $"product:{productId}", branchId, limit, nowUtc, cancellationToken);

        if (similar.Items.Count > 0)
        {
            return Results.Ok(new RecommendationResponse(
                nameof(RecommendationScope.SimilarProduct), similar.GeneratedAtUtc, similar.Items));
        }

        var global = await LoadScopeItemsAsync(
            dbContext, jobRunId.Value, RecommendationScope.Global,
            "global", branchId, limit, nowUtc, cancellationToken);

        if (global.Items.Count == 0)
        {
            return Results.Ok(new RecommendationResponse(null, null, []));
        }

        return Results.Ok(new RecommendationResponse(
            nameof(RecommendationScope.Global), global.GeneratedAtUtc, global.Items));
    }

    private static async Task<IResult> GetAdminRecommendationResultsAsync(
        [FromQuery] string? scope,
        [FromQuery] int limit = 10,
        [FromServices] AppDbContext dbContext = null!,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1 || limit > 50)
        {
            return Results.BadRequest(new { message = "Limit must be between 1 and 50." });
        }

        if (!string.IsNullOrWhiteSpace(scope)
            && !Enum.TryParse<RecommendationScope>(scope, true, out var _))
        {
            return Results.BadRequest(new { message = "Invalid recommendation scope." });
        }

        var jobRunId = await LatestSucceededRunIdAsync(dbContext, cancellationToken);
        if (jobRunId == null)
        {
            return Results.Ok(new RecommendationSampleResponse(
                Guid.Empty, DateTime.MinValue, DateTime.MinValue, string.Empty, []));
        }

        var nowUtc = DateTime.UtcNow;
        var query = dbContext.RecommendationResults.AsNoTracking()
            .Where(result => result.JobRunId == jobRunId.Value && result.ExpiresAtUtc > nowUtc);

        if (!string.IsNullOrWhiteSpace(scope)
            && Enum.TryParse<RecommendationScope>(scope, true, out var scopeValue))
        {
            query = query.Where(result => result.Scope == scopeValue);
        }

        var rows = await query
            .OrderBy(result => result.Scope)
            .ThenBy(result => result.Rank)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var generatedAt = rows.Count > 0 ? rows[0].GeneratedAtUtc : DateTime.MinValue;
        var expiresAt = rows.Count > 0 ? rows[0].ExpiresAtUtc : DateTime.MinValue;
        var algorithmVersion = rows.Count > 0 ? rows[0].AlgorithmVersion : string.Empty;

        var items = rows
            .Select(result => new RecommendationSampleItemDto(
                result.RecommendedProductId,
                result.Scope.ToString(),
                result.AudienceKey,
                result.Score,
                result.Rank,
                result.Reason))
            .ToList();

        return Results.Ok(new RecommendationSampleResponse(
            jobRunId.Value, generatedAt!, expiresAt!, algorithmVersion, items));
    }

    private static async Task<IResult> TriggerRecommendationRunAsync(
        [FromServices] JobRunCoordinator coordinator,
        [FromServices] AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var run = await coordinator.TryQueueAsync("Recommendations", "global", cancellationToken);
        if (run == null)
        {
            return Results.Conflict(new { message = "A recommendation run is already active." });
        }

        var statusUrl = $"/api/admin/jobs/{run.Value}";
        return Results.Accepted(
            statusUrl,
            new TriggerRecommendationRunResponse(run.Value, statusUrl));
    }

    private static async Task<Guid?> LatestSucceededRunIdAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await dbContext.BackgroundJobRuns.AsNoTracking()
            .Where(run => run.JobName == "Recommendations" && run.Status == JobRunStatus.Succeeded)
            .OrderByDescending(run => run.CompletedAtUtc ?? run.CreatedAtUtc)
            .Select(run => run.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private sealed record ScopedItems(DateTime? GeneratedAtUtc, IReadOnlyList<RecommendationItemDto> Items);

    private static async Task<ScopedItems> LoadScopeItemsAsync(
        AppDbContext dbContext,
        Guid jobRunId,
        RecommendationScope scope,
        string audienceKey,
        Guid? branchId,
        int limit,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.RecommendationResults.AsNoTracking()
            .Where(result => result.JobRunId == jobRunId
                && result.Scope == scope
                && result.AudienceKey == audienceKey
                && result.ExpiresAtUtc > nowUtc)
            .OrderBy(result => result.Rank)
            .Take(limit * 4)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return new ScopedItems(null, []);
        }

        var generatedAtUtc = rows[0].GeneratedAtUtc;
        var productIds = rows.Select(row => row.RecommendedProductId).Distinct().ToArray();

        var products = await dbContext.Products.AsNoTracking()
            .Include(product => product.Brand)
            .Where(product => product.IsActive
                && product.Brand!.IsActive
                && productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        Dictionary<Guid, int> availability = [];
        if (branchId.HasValue)
        {
            availability = await dbContext.BranchInventories.AsNoTracking()
                .Where(inventory => inventory.BranchId == branchId.Value
                    && productIds.Contains(inventory.ProductId))
                .ToDictionaryAsync(inventory => inventory.ProductId,
                    inventory => inventory.AvailableQuantity, cancellationToken);
        }

        var items = new List<RecommendationItemDto>();
        foreach (var row in rows)
        {
            if (items.Count >= limit)
            {
                break;
            }

            if (!products.TryGetValue(row.RecommendedProductId, out var product))
            {
                continue;
            }

            if (branchId.HasValue)
            {
                if (!availability.TryGetValue(product.Id, out var availableQuantity) || availableQuantity <= 0)
                {
                    continue;
                }

                items.Add(new RecommendationItemDto(
                    product.Id, product.Name, product.Slug, product.ImageUrl,
                    product.BasePrice, availableQuantity, row.Score, row.Reason));
            }
            else
            {
                items.Add(new RecommendationItemDto(
                    product.Id, product.Name, product.Slug, product.ImageUrl,
                    product.BasePrice, null, row.Score, row.Reason));
            }
        }

        return new ScopedItems(generatedAtUtc, items);
    }
}