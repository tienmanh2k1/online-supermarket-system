using System.Data;
using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Payments;
using OnlineSupermarket.Infrastructure.Persistence;
using OnlineSupermarket.Infrastructure.Inventory;

namespace OnlineSupermarket.Infrastructure.Payments;

public sealed class PaymentCallbackProcessor(AppDbContext dbContext, IInventoryMutationService mutationService) : IPaymentCallbackProcessor
{
    public async Task<PaymentCallbackOutcome> ProcessAsync(string provider, PaymentCallbackVerificationResult callback, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var duplicate = await dbContext.PaymentCallbacks.AnyAsync(x => x.Provider == provider && x.ExternalEventId == callback.ExternalEventId, cancellationToken);
        if (duplicate) return PaymentCallbackOutcome.AlreadyProcessed;

        var payment = await dbContext.Payments.FirstOrDefaultAsync(x => x.OrderId == callback.OrderId, cancellationToken);
        if (payment is null) return PaymentCallbackOutcome.PaymentNotFound;
        var expectedMethod = provider.Equals("VNPay", StringComparison.OrdinalIgnoreCase) ? PaymentMethod.VNPay : PaymentMethod.MoMo;
        if (payment.Method != expectedMethod || payment.Amount != callback.Amount || payment.Status is PaymentStatus.Completed or PaymentStatus.Failed or PaymentStatus.Refunded)
            return PaymentCallbackOutcome.Conflict;

        dbContext.PaymentCallbacks.Add(PaymentCallback.Create(payment.Id, provider, callback.ExternalEventId, callback.SanitizedPayload, true, callback.Amount, callback.IsSuccess ? PaymentStatus.Completed : PaymentStatus.Failed));
        var order = await dbContext.Orders.Include(x => x.Items).Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == payment.OrderId, cancellationToken);
        if (order is null) return PaymentCallbackOutcome.PaymentNotFound;
        if (callback.IsSuccess)
        {
            payment.MarkCompleted(callback.ExternalEventId, callback.SanitizedPayload);
            order.SetStatus(OrderStatus.Confirmed, "Payment confirmed");
        }
        else
        {
            payment.MarkFailed(callback.SanitizedPayload);
            var productIds = order.Items.Select(x => x.ProductId).Distinct().ToArray();
            var inventories = await dbContext.BranchInventories.Where(x => x.BranchId == order.BranchId && productIds.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId, cancellationToken);
            var commands = order.Items.Where(x => inventories.ContainsKey(x.ProductId))
                .Select(x => InventoryMutationCommand.Release(inventories[x.ProductId].Id, x.Quantity, order.Id)).ToArray();
            await mutationService.ApplyBatchAsync(commands, cancellationToken);
            order.SetStatus(OrderStatus.Cancelled, "Payment failed - inventory released");
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PaymentCallbackOutcome.Processed;
    }
}
