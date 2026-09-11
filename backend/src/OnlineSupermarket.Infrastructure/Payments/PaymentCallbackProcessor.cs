using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Payments;
using OnlineSupermarket.Infrastructure.Inventory;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Payments;

public sealed class PaymentCallbackProcessor(
    AppDbContext dbContext,
    IInventoryMutationService mutationService,
    IOptions<PaymentOptions>? paymentOptions = null,
    IHostEnvironment? environment = null) : IPaymentCallbackProcessor
{
    private const int MaxDeadlockRetries = 3;

    public async Task<PaymentCallbackOutcome> ProcessAsync(
        string provider,
        PaymentCallbackVerificationResult callback,
        CancellationToken cancellationToken)
    {
        if (!callback.IsValidSignature && !callback.IsMock)
            throw new InvalidOperationException("Callback must be signature-verified before processing.");

        if (callback.IsMock && (environment?.IsDevelopment() != true || paymentOptions?.Value.Mode != "Mock" || !callback.TargetPaymentId.HasValue))
            return PaymentCallbackOutcome.Conflict;

        // Synchronize contenders before any locks, and only once across retries.
        if (BeforeOrderLockTestHook is not null)
            await BeforeOrderLockTestHook();

        for (var attempt = 0; attempt < MaxDeadlockRetries; attempt++)
        {
            try
            {
                return await ProcessOnceAsync(provider, callback, cancellationToken);
            }
            catch (Exception error) when (IsDeadlockError(error))
            {
                dbContext.ChangeTracker.Clear();
                if (attempt == MaxDeadlockRetries - 1)
                    throw;
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), cancellationToken);
            }
        }

        throw new InvalidOperationException("Payment callback could not be processed after retries.");
    }

    private async Task<PaymentCallbackOutcome> ProcessOnceAsync(
        string provider,
        PaymentCallbackVerificationResult callback,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        // Lock the order before reading payments: SERIALIZABLE reads can also lock.
        await LockOrderAsync(callback.OrderId, cancellationToken);
        var order = await dbContext.Orders
            .Include(x => x.Items)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == callback.OrderId, cancellationToken);
        if (order is null) return PaymentCallbackOutcome.PaymentNotFound;
        await dbContext.Entry(order).ReloadAsync(cancellationToken);

        var paymentQuery = dbContext.Payments.Where(x => x.OrderId == callback.OrderId);
        if (callback.TargetPaymentId.HasValue)
            paymentQuery = paymentQuery.Where(x => x.Id == callback.TargetPaymentId.Value);
        var candidate = await paymentQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new { x.Id, x.OrderId })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null) return PaymentCallbackOutcome.PaymentNotFound;

        // Row-lock the selected payment so a concurrent conflicting callback
        // (different externalEventId) serializes here and observes the
        // committed terminal state instead of both writing a reversal.
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Payments
                .FromSqlInterpolated($"SELECT * FROM payments WHERE Id = {candidate.Id} FOR UPDATE")
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);
        }
        var payment = await dbContext.Payments.FirstOrDefaultAsync(x => x.Id == candidate.Id, cancellationToken);
        if (payment is null) return PaymentCallbackOutcome.PaymentNotFound;
        // Ownership checks may have tracked a Pending entity before we acquired locks.
        await dbContext.Entry(payment).ReloadAsync(cancellationToken);

        // We may have blocked on the row lock while the winning transaction
        // committed the very callback we are about to insert; re-check the
        // unique (provider, externalEventId) after serialization.
        var duplicateAfterLock = await dbContext.PaymentCallbacks.AnyAsync(
            x => x.PaymentId == payment.Id && x.Provider == provider && x.ExternalEventId == callback.ExternalEventId,
            cancellationToken);
        if (duplicateAfterLock) return PaymentCallbackOutcome.AlreadyProcessed;

        var expectedMethod = PaymentMethodExpected(provider);
        if (expectedMethod is null
            || payment.Method != expectedMethod
            || payment.IsMock != callback.IsMock
            || payment.Amount != callback.Amount
            || payment.Status is PaymentStatus.Completed or PaymentStatus.Failed or PaymentStatus.Refunded)
        {
            return PaymentCallbackOutcome.Conflict;
        }

        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
            return PaymentCallbackOutcome.Conflict;

        dbContext.PaymentCallbacks.Add(PaymentCallback.Create(
            payment.Id, provider, callback.ExternalEventId, callback.SanitizedPayload,
            callback.IsValidSignature, callback.Amount, callback.IsSuccess ? PaymentStatus.Completed : PaymentStatus.Failed));

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

        if (BeforeSaveTestHook is not null)
            await BeforeSaveTestHook();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (IsDuplicateKeyError(error))
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
            dbContext.ChangeTracker.Clear();
            var winner = await dbContext.PaymentCallbacks.AnyAsync(
                x => x.Provider == provider && x.ExternalEventId == callback.ExternalEventId, cancellationToken);
            if (winner)
                return PaymentCallbackOutcome.AlreadyProcessed;
            throw;
        }

        await transaction.CommitAsync(cancellationToken);
        return PaymentCallbackOutcome.Processed;
    }

    internal Func<Task>? BeforeSaveTestHook { get; set; }
    internal Func<Task>? BeforeOrderLockTestHook { get; set; }

    private async Task LockOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational()) return;
        await dbContext.Orders
            .FromSqlInterpolated($"SELECT * FROM orders WHERE Id = {orderId} FOR UPDATE")
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static bool IsDuplicateKeyError(Exception error)
    {
        Exception? current = error;
        while (current is not null)
        {
            if (current is MySqlException { Number: 1062 })
                return true;
            var message = current.Message ?? string.Empty;
            if (message.Contains("1062", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            current = current.InnerException;
        }
        return false;
    }

    private static bool IsDeadlockError(Exception error)
    {
        Exception? current = error;
        while (current is not null)
        {
            if (current is MySqlException { Number: 1213 })
                return true;
            var message = current.Message ?? string.Empty;
            if (message.Contains("1213", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Deadlock found", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            current = current.InnerException;
        }
        return false;
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
        var inventories = new Dictionary<Guid, BranchInventory>();
        foreach (var productId in productIds)
        {
            var inventory = await dbContext.BranchInventories
                .FirstOrDefaultAsync(x => x.BranchId == order.BranchId && x.ProductId == productId, cancellationToken);
            if (inventory is not null) inventories[inventory.ProductId] = inventory;
        }

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
