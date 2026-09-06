using System.Data;
using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Payments;
using OnlineSupermarket.Infrastructure.Inventory;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Payments;

public sealed class PaymentCallbackProcessor(AppDbContext dbContext, IInventoryMutationService mutationService) : IPaymentCallbackProcessor
{
    public async Task<PaymentCallbackOutcome> ProcessAsync(
        string provider,
        PaymentCallbackVerificationResult callback,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var duplicate = await dbContext.PaymentCallbacks.AnyAsync(
            x => x.Provider == provider && x.ExternalEventId == callback.ExternalEventId, cancellationToken);
        if (duplicate) return PaymentCallbackOutcome.AlreadyProcessed;

        var payment = await dbContext.Payments
            .Where(x => x.OrderId == callback.OrderId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (payment is null) return PaymentCallbackOutcome.PaymentNotFound;

        var expectedMethod = PaymentMethodExpected(provider);
        if (expectedMethod is null
            || payment.Method != expectedMethod
            || payment.Amount != callback.Amount
            || payment.Status is PaymentStatus.Completed or PaymentStatus.Failed or PaymentStatus.Refunded)
        {
            return PaymentCallbackOutcome.Conflict;
        }

        var order = await dbContext.Orders
            .Include(x => x.Items)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == payment.OrderId, cancellationToken);
        if (order is null) return PaymentCallbackOutcome.PaymentNotFound;

        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
            return PaymentCallbackOutcome.Conflict;

        dbContext.PaymentCallbacks.Add(PaymentCallback.Create(
            payment.Id, provider, callback.ExternalEventId, callback.SanitizedPayload,
            true, callback.Amount, callback.IsSuccess ? PaymentStatus.Completed : PaymentStatus.Failed));

        if (callback.IsSuccess)
        {
            payment.MarkCompleted(callback.ExternalEventId, callback.SanitizedPayload);
            if (order.Status == OrderStatus.Pending)
                order.SetStatus(OrderStatus.Confirmed, "Payment confirmed");
        }
        else
        {
            payment.MarkFailed(callback.SanitizedPayload);
            await ReleaseInventoryAsync(order, cancellationToken);
            await ReleasePromotionUsageAsync(order, cancellationToken);
            order.SetStatus(OrderStatus.Cancelled, "Payment failed - inventory released");
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            var duplicateEntry = ex.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase);
            await transaction.RollbackAsync(cancellationToken);
            if (duplicateEntry)
                return PaymentCallbackOutcome.AlreadyProcessed;
            throw;
        }

        await transaction.CommitAsync(cancellationToken);
        return PaymentCallbackOutcome.Processed;
    }

    private static PaymentMethod? PaymentMethodExpected(string provider)
        => provider.Equals("VNPay", StringComparison.OrdinalIgnoreCase)
            ? PaymentMethod.VNPay
            : provider.Equals("MoMo", StringComparison.OrdinalIgnoreCase)
                ? PaymentMethod.MoMo
                : null;

    private async Task ReleaseInventoryAsync(Order order, CancellationToken cancellationToken)
    {
        var productIds = order.Items.Select(x => x.ProductId).Distinct().ToArray();
        var inventories = await dbContext.BranchInventories
            .Where(x => x.BranchId == order.BranchId && productIds.Contains(x.ProductId))
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);

        var commands = order.Items
            .GroupBy(x => x.ProductId)
            .Select(g =>
            {
                if (!inventories.TryGetValue(g.Key, out var inventory))
                    throw new InvalidOperationException(
                        $"Missing inventory for product {g.Key} in branch {order.BranchId}; " +
                        "payment failure callback must roll back.");
                return InventoryMutationCommand.Release(inventory.Id, g.Sum(x => x.Quantity), order.Id);
            })
            .ToArray();

        await mutationService.ApplyBatchAsync(commands, cancellationToken);
    }

    private async Task ReleasePromotionUsageAsync(Order order, CancellationToken cancellationToken)
    {
        if (!order.PromotionId.HasValue) return;
        var promotion = await dbContext.Promotions.FirstOrDefaultAsync(
            x => x.Id == order.PromotionId.Value, cancellationToken);
        if (promotion is not null)
            promotion.ReleaseUsage();
    }
}