using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Api.Contracts.Review;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Reviews;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder routes)
    {
        var reviewsGroup = routes.MapGroup("/api/reviews")
            .WithTags("Reviews");

        reviewsGroup.MapPost("/", CreateReviewAsync)
            .RequireAuthorization()
            .WithName("CreateReview")
            .Produces<ReviewDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        reviewsGroup.MapPut("/{id:guid}", UpdateReviewAsync)
            .RequireAuthorization()
            .WithName("UpdateReview")
            .Produces<ReviewDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        reviewsGroup.MapGet("/{id:guid}", GetReviewByIdAsync)
            .AllowAnonymous()
            .WithName("GetReviewById")
            .Produces<ReviewDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var productReviewsGroup = routes.MapGroup("/api/products/{productId:guid}")
            .WithTags("Reviews");

        productReviewsGroup.MapGet("/reviews", GetProductReviewsAsync)
            .AllowAnonymous()
            .WithName("GetProductReviews")
            .Produces<ProductReviewsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        productReviewsGroup.MapGet("/review-eligibility", GetReviewEligibilityAsync)
            .WithName("GetReviewEligibility")
            .Produces<ReviewEligibilityDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateReviewAsync(
        [FromBody] CreateReviewRequest request,
        ClaimsPrincipal user,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);

        if (request.Rating < 1 || request.Rating > 5)
        {
            return Results.BadRequest(new { message = "RATING_OUT_OF_RANGE" });
        }

        if (request.Comment is not null && request.Comment.Trim().Length > 2000)
        {
            return Results.BadRequest(new { message = "COMMENT_TOO_LONG" });
        }

        var orderItem = await (
            from oi in dbContext.OrderItems
            join o in dbContext.Orders on oi.OrderId equals o.Id
            where oi.Id == request.OrderItemId
            select new { oi.Id, oi.ProductId, o.UserId, o.Status }
        ).FirstOrDefaultAsync(cancellationToken);

        if (orderItem is null)
        {
            return Results.NotFound(new { message = "ORDER_ITEM_NOT_FOUND" });
        }

        if (orderItem.UserId != userId)
        {
            return Results.Forbid();
        }

        if (orderItem.Status != OrderStatus.Completed)
        {
            return Results.Conflict(new { message = "ORDER_NOT_COMPLETED" });
        }

        var productExists = await dbContext.Products
            .AnyAsync(p => p.Id == orderItem.ProductId && p.IsActive, cancellationToken);
        if (!productExists)
        {
            return Results.NotFound(new { message = "PRODUCT_NOT_FOUND" });
        }

        var alreadyReviewed = await dbContext.Reviews
            .AnyAsync(r => r.OrderItemId == request.OrderItemId, cancellationToken);
        if (alreadyReviewed)
        {
            return Results.Conflict(new { message = "REVIEW_ALREADY_EXISTS" });
        }

        var review = Review.Create(
            userId,
            orderItem.Id,
            orderItem.ProductId,
            request.Rating,
            request.Comment);

        dbContext.Reviews.Add(review);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueOrderItemViolation(ex))
        {
            return Results.Conflict(new { message = "REVIEW_ALREADY_EXISTS" });
        }

        var userEntity = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        var reviewerName = string.IsNullOrWhiteSpace(userEntity?.FullName)
            ? "Khách hàng"
            : userEntity.FullName;

        var dto = new ReviewDto(
            review.Id,
            review.ProductId,
            reviewerName,
            review.Rating,
            review.Comment,
            review.CreatedAtUtc,
            review.UpdatedAtUtc);

        return Results.Created($"/api/reviews/{review.Id}", dto);
    }

    private static async Task<IResult> UpdateReviewAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateReviewRequest request,
        ClaimsPrincipal user,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);

        if (request.Rating < 1 || request.Rating > 5)
        {
            return Results.BadRequest(new { message = "RATING_OUT_OF_RANGE" });
        }

        if (request.Comment is not null && request.Comment.Trim().Length > 2000)
        {
            return Results.BadRequest(new { message = "COMMENT_TOO_LONG" });
        }

        var review = await dbContext.Reviews
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (review is null)
        {
            return Results.NotFound(new { message = "REVIEW_NOT_FOUND" });
        }

        if (review.UserId != userId)
        {
            return Results.Forbid();
        }

        review.Update(request.Rating, request.Comment);

        await dbContext.SaveChangesAsync(cancellationToken);

        var userEntity = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        var reviewerName = string.IsNullOrWhiteSpace(userEntity?.FullName)
            ? "Khách hàng"
            : userEntity.FullName;

        var dto = new ReviewDto(
            review.Id,
            review.ProductId,
            reviewerName,
            review.Rating,
            review.Comment,
            review.CreatedAtUtc,
            review.UpdatedAtUtc);

        return Results.Ok(dto);
    }

    private static async Task<IResult> GetProductReviewsAsync(
        [FromRoute] Guid productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        AppDbContext dbContext = default!,
        CancellationToken cancellationToken = default)
    {
        var productExists = await dbContext.Products
            .AnyAsync(p => p.Id == productId && p.IsActive, cancellationToken);
        if (!productExists)
        {
            return Results.NotFound(new { message = "PRODUCT_NOT_FOUND" });
        }

        var safePageSize = Math.Clamp(pageSize, 1, 50);

        var query = dbContext.Reviews.Where(r => r.ProductId == productId);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)safePageSize));
        var safePage = Math.Clamp(page, 1, totalPages);

        decimal averageRating = 0;
        if (totalCount > 0)
        {
            var avg = await query.AverageAsync(r => (decimal)r.Rating, cancellationToken);
            averageRating = Math.Round(avg, 1);
        }

        var reviewsWithUsers = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Join(
                dbContext.Users,
                r => r.UserId,
                u => u.Id,
                (r, u) => new { Review = r, ReviewerName = u.FullName })
            .ToListAsync(cancellationToken);

        var items = reviewsWithUsers.Select(x => new ReviewDto(
            x.Review.Id,
            x.Review.ProductId,
            string.IsNullOrWhiteSpace(x.ReviewerName) ? "Khách hàng" : x.ReviewerName,
            x.Review.Rating,
            x.Review.Comment,
            x.Review.CreatedAtUtc,
            x.Review.UpdatedAtUtc)).ToList();

        var result = new ProductReviewsDto(
            AverageRating: averageRating,
            ReviewCount: totalCount,
            Data: items,
            Page: safePage,
            PageSize: safePageSize,
            TotalCount: totalCount);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetReviewEligibilityAsync(
        [FromRoute] Guid productId,
        [FromQuery] Guid? orderItemId,
        ClaimsPrincipal? user,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var productExists = await dbContext.Products
            .AnyAsync(p => p.Id == productId && p.IsActive, cancellationToken);
        if (!productExists)
        {
            return Results.NotFound(new { message = "PRODUCT_NOT_FOUND" });
        }

        var userId = TryGetUserId(user);
        if (!userId.HasValue)
        {
            return Results.Ok(new ReviewEligibilityDto(CanReview: false, OrderItemId: null, ReviewId: null));
        }

        if (orderItemId.HasValue)
        {
            var target = await (from oi in dbContext.OrderItems
                                join o in dbContext.Orders on oi.OrderId equals o.Id
                                where oi.Id == orderItemId.Value
                                select new { oi.ProductId, o.UserId, o.Status }).FirstOrDefaultAsync(cancellationToken);
            if (target is not null && target.ProductId != productId)
                return Results.BadRequest(new { code = "ORDER_ITEM_PRODUCT_MISMATCH" });
            if (target is null || target.UserId != userId.Value || target.Status != OrderStatus.Completed)
                return Results.Ok(new ReviewEligibilityDto(false, null, null));
            var alreadyReviewed = await dbContext.Reviews.AnyAsync(x => x.OrderItemId == orderItemId.Value, cancellationToken);
            return alreadyReviewed
                ? Results.Ok(new ReviewEligibilityDto(false, null, null))
                : Results.Ok(new ReviewEligibilityDto(true, orderItemId, null));
        }

        var completedItems = await (
            from oi in dbContext.OrderItems
            join o in dbContext.Orders on oi.OrderId equals o.Id
            where oi.ProductId == productId
               && o.UserId == userId.Value
               && o.Status == OrderStatus.Completed
            orderby o.CreatedAtUtc descending
            select new { oi.Id, o.CreatedAtUtc }
        ).ToListAsync(cancellationToken);

        if (completedItems.Count == 0)
        {
            return Results.Ok(new ReviewEligibilityDto(CanReview: false, OrderItemId: null, ReviewId: null));
        }

        var itemIds = completedItems.Select(x => x.Id).ToList();
        var existingReviews = new List<Review>();
        foreach (var itemId in itemIds)
        {
            var review = await dbContext.Reviews
                .FirstOrDefaultAsync(r => r.OrderItemId == itemId, cancellationToken);
            if (review is not null) existingReviews.Add(review);
        }

        var reviewedItemIds = existingReviews.Select(r => r.OrderItemId).ToHashSet();
        var eligibleItem = completedItems.FirstOrDefault(x => !reviewedItemIds.Contains(x.Id));

        if (eligibleItem is not null)
        {
            return Results.Ok(new ReviewEligibilityDto(CanReview: true, OrderItemId: eligibleItem.Id, ReviewId: null));
        }

        var latestReview = existingReviews
            .OrderByDescending(r => r.UpdatedAtUtc)
            .FirstOrDefault();

        return Results.Ok(new ReviewEligibilityDto(
            CanReview: false,
            OrderItemId: null,
            ReviewId: latestReview?.Id,
            ExistingRating: latestReview?.Rating,
            ExistingComment: latestReview?.Comment));
    }

    private static async Task<IResult> GetReviewByIdAsync(
        [FromRoute] Guid id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var reviewWithUser = await (
            from r in dbContext.Reviews
            join p in dbContext.Products on r.ProductId equals p.Id
            join u in dbContext.Users on r.UserId equals u.Id into users
            from u in users.DefaultIfEmpty()
            where r.Id == id && p.IsActive
            select new { Review = r, ReviewerName = u != null ? u.FullName : null }
        ).FirstOrDefaultAsync(cancellationToken);

        if (reviewWithUser is null)
        {
            return Results.NotFound(new { message = "REVIEW_NOT_FOUND" });
        }

        var reviewerName = string.IsNullOrWhiteSpace(reviewWithUser.ReviewerName)
            ? "Khách hàng"
            : reviewWithUser.ReviewerName;

        var dto = new ReviewDto(
            reviewWithUser.Review.Id,
            reviewWithUser.Review.ProductId,
            reviewerName,
            reviewWithUser.Review.Rating,
            reviewWithUser.Review.Comment,
            reviewWithUser.Review.CreatedAtUtc,
            reviewWithUser.Review.UpdatedAtUtc);

        return Results.Ok(dto);
    }

    public static bool IsUniqueOrderItemViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        var isDuplicate = message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase)
            || message.Contains("1062", StringComparison.OrdinalIgnoreCase);
        var isOrderItemConstraint = message.Contains("ix_reviews_order_item_id", StringComparison.OrdinalIgnoreCase)
            || message.Contains("order_item_id", StringComparison.OrdinalIgnoreCase);

        return isDuplicate && isOrderItemConstraint;
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)
            ?? user.FindFirst("sub");
        if (claim is null || !Guid.TryParse(claim.Value, out var userId))
        {
            throw new InvalidOperationException("User id not found in token claims.");
        }
        return userId;
    }

    private static Guid? TryGetUserId(ClaimsPrincipal? user)
    {
        if (user is null) return null;
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)
            ?? user.FindFirst("sub");
        if (claim is not null && Guid.TryParse(claim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }
}
