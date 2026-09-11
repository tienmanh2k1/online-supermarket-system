using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Api.Contracts.Order;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Payments;
using OnlineSupermarket.Infrastructure.Inventory;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder routes)
    {
        var customerGroup = routes.MapGroup("/api/orders")
            .WithTags("Orders")
            .RequireAuthorization();

        customerGroup.MapGet("/", GetOrdersAsync)
            .WithName("GetOrders")
            .Produces<PaginatedOrdersDto>();

        customerGroup.MapGet("/{id:guid}", GetOrderByIdAsync)
            .WithName("GetOrderById")
            .Produces<OrderDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var adminGroup = routes.MapGroup("/api/admin/orders")
            .WithTags("Admin-Orders")
            .RequireAuthorization("AdminOnly");

        adminGroup.MapGet("/", GetAllOrdersAsync)
            .WithName("GetAllOrders")
            .Produces<PaginatedOrdersDto>();

        adminGroup.MapGet("/{id:guid}", GetOrderByIdForAdminAsync)
            .WithName("GetOrderByIdForAdmin")
            .Produces<OrderDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        adminGroup.MapPut("/{id:guid}/status", UpdateOrderStatusAsync)
            .WithName("UpdateOrderStatus")
            .Produces<OrderDetailDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return routes;
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId
            : throw new UnauthorizedAccessException("Invalid user token.");
    }

    private static async Task<IResult> GetOrdersAsync(
        ClaimsPrincipal user,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromServices] AppDbContext dbContext = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId(user);
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 50) pageSize = 50;

        var query = dbContext.Orders.Where(o => o.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var statusEnum))
            query = query.Where(o => o.Status == statusEnum);

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderListDto(
                o.Id, o.CreatedAtUtc, o.TotalAmount, o.Status.ToString(), o.FulfillmentType, o.Items.Count))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PaginatedOrdersDto(orders, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetOrderByIdAsync(
        ClaimsPrincipal user,
        [FromRoute] Guid id,
        [FromServices] AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);

        var order = await dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId, cancellationToken);

        if (order == null)
            return Results.NotFound(new { message = "Order not found." });

        return Results.Ok(await MapToDetailDtoAsync(order, dbContext, cancellationToken));
    }

    private static async Task<IResult> GetAllOrdersAsync(
        [FromQuery] string? status,
        [FromQuery] Guid? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromServices] AppDbContext dbContext = null!,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var query = dbContext.Orders.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var statusEnum))
            query = query.Where(o => o.Status == statusEnum);

        if (userId.HasValue)
            query = query.Where(o => o.UserId == userId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderListDto(
                o.Id, o.CreatedAtUtc, o.TotalAmount, o.Status.ToString(), o.FulfillmentType, o.Items.Count))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PaginatedOrdersDto(orders, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetOrderByIdForAdminAsync(
        [FromRoute] Guid id,
        [FromServices] AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order == null)
            return Results.NotFound(new { message = "Order not found." });

        return Results.Ok(await MapToDetailDtoAsync(order, dbContext, cancellationToken));
    }

    private static async Task<IResult> UpdateOrderStatusAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        [FromServices] AppDbContext dbContext,
        [FromServices] IInventoryMutationService mutationService,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var newStatus))
            return Results.BadRequest(new { message = "Invalid status value." });

        var order = await dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order == null)
            return Results.NotFound(new { message = "Order not found." });

        var payment = await dbContext.Payments
            .Where(item => item.OrderId == order.Id)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (newStatus != OrderStatus.Cancelled
            && (payment is null || (payment.Method != PaymentMethod.COD && payment.Status != PaymentStatus.Completed)))
            return Results.Conflict(new { code = "PAYMENT_NOT_COMPLETED" });
        if (newStatus == OrderStatus.Cancelled && payment is { IsMock: true, Status: PaymentStatus.Completed })
            return Results.Conflict(new { code = "REFUND_NOT_SUPPORTED" });

        var validTransitions = GetValidTransitions(order.Status);
        if (!validTransitions.Contains(newStatus))
            return Results.BadRequest(new { message = $"Invalid transition from {order.Status} to {newStatus}." });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        if (newStatus == OrderStatus.Cancelled)
        {
            var releaseCommands = await OrderItemCommandsAsync(
                order, dbContext, (id, quantity, orderId) => InventoryMutationCommand.Release(id, quantity, orderId), cancellationToken);
            await mutationService.ApplyBatchAsync(releaseCommands, cancellationToken);

            if (order.PromotionId.HasValue)
            {
                var promotion = await dbContext.Promotions
                    .FirstOrDefaultAsync(p => p.Id == order.PromotionId.Value, cancellationToken);
                promotion?.ReleaseUsage();
            }

            if (payment is { IsMock: true, Status: PaymentStatus.Pending })
                payment.MarkFailed("{\"mode\":\"Mock\",\"outcome\":\"Cancelled\",\"reason\":\"AdminCancelled\"}");
        }
        else if (newStatus == OrderStatus.Completed)
        {
            var saleCommands = await OrderItemCommandsAsync(
                order, dbContext, (id, quantity, orderId) => InventoryMutationCommand.Sale(id, quantity, orderId), cancellationToken);
            await mutationService.ApplyBatchAsync(saleCommands, cancellationToken);

            // Mark COD payment as completed (money collected when order delivered)
            var codPayment = dbContext.Payments
                .FirstOrDefault(p => p.OrderId == order.Id && p.Method == PaymentMethod.COD && p.Status == PaymentStatus.PendingCollection);

            if (codPayment != null)
            {
                codPayment.MarkCompleted($"COD-{order.Id}", "Order completed - payment collected");
                dbContext.Entry(codPayment).State = EntityState.Modified;
            }
        }

        order.SetStatus(newStatus, request.Note);

        // Force EF Core to track the new history entry added by SetStatus
        var historyEntries = order.StatusHistory;
        if (historyEntries.Count > 0)
        {
            var latestHistory = historyEntries[^1];
            dbContext.Entry(latestHistory).State = EntityState.Added;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(await MapToDetailDtoAsync(order, dbContext, cancellationToken));
    }

    private static async Task<IReadOnlyCollection<InventoryMutationCommand>> OrderItemCommandsAsync(
        Order order,
        AppDbContext dbContext,
        Func<Guid, int, Guid, InventoryMutationCommand> factory,
        CancellationToken cancellationToken)
    {
        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToArray();
        if (productIds.Length == 0)
        {
            return [];
        }

        var inventories = new Dictionary<Guid, BranchInventory>();
        foreach (var productId in productIds)
        {
            var inventory = await dbContext.BranchInventories
                .FirstOrDefaultAsync(bi => bi.BranchId == order.BranchId && bi.ProductId == productId, cancellationToken);
            if (inventory is not null) inventories[inventory.ProductId] = inventory;
        }

        return order.Items
            .Where(i => inventories.ContainsKey(i.ProductId))
            .Select(i => factory(inventories[i.ProductId].Id, i.Quantity, order.Id))
            .ToArray();
    }

    private static HashSet<OrderStatus> GetValidTransitions(OrderStatus current)
    {
        return current switch
        {
            OrderStatus.Pending => new() { OrderStatus.Confirmed, OrderStatus.Cancelled },
            OrderStatus.Confirmed => new() { OrderStatus.Preparing, OrderStatus.Cancelled },
            OrderStatus.Preparing => new() { OrderStatus.Ready, OrderStatus.Shipped, OrderStatus.Cancelled },
            OrderStatus.Ready => new() { OrderStatus.Shipped, OrderStatus.Delivered },
            OrderStatus.Shipped => new() { OrderStatus.Delivered },
            OrderStatus.Delivered => new() { OrderStatus.Completed },
            OrderStatus.Completed => new(),
            OrderStatus.Cancelled => new(),
            OrderStatus.Failed => new(),
            _ => new()
        };
    }

    private static async Task<OrderDetailDto> MapToDetailDtoAsync(
        Order order,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .Where(p => p.OrderId == order.Id)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var itemIds = order.Items.Select(i => i.Id).ToList();
        var reviews = await dbContext.Reviews
            .Where(r => itemIds.Contains(r.OrderItemId))
            .Select(r => new { r.OrderItemId, r.Id })
            .ToDictionaryAsync(r => r.OrderItemId, r => r.Id, cancellationToken);

        var isCompleted = order.Status == OrderStatus.Completed;

        var itemDtos = order.Items.Select(i =>
        {
            var hasReview = reviews.TryGetValue(i.Id, out var reviewId);
            return new OrderItemDto(
                i.Id,
                i.ProductId,
                i.ProductName,
                i.Sku,
                i.UnitPrice,
                i.Quantity,
                i.LineTotal,
                CanReview: isCompleted && !hasReview,
                ReviewId: hasReview ? reviewId : null);
        }).ToList();

        var historyDtos = order.StatusHistory.Select(h => new StatusHistoryDto(
            h.FromStatus.ToString(), h.ToStatus.ToString(), h.Note, h.CreatedAtUtc)).ToList();

        PaymentDto? paymentDto = null;
        if (payment != null)
        {
            paymentDto = new PaymentDto(
                payment.Id, payment.Method.ToString(), payment.Status.ToString(),
                payment.Amount, payment.ProviderTransactionId, payment.CreatedAtUtc, payment.IsMock);
        }

        return new OrderDetailDto(
            order.Id, order.UserId, order.BranchId, order.FulfillmentType,
            order.RecipientName, order.RecipientPhone, order.DeliveryAddressSnapshot,
            order.Subtotal, order.DiscountAmount, order.ShippingFee, order.TotalAmount,
            order.PromotionCodeSnapshot, order.Status.ToString(),
            order.CreatedAtUtc, order.UpdatedAtUtc, itemDtos, historyDtos, paymentDto);
    }
}
