using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Payments;
using OnlineSupermarket.Domain.Promotions;
using OnlineSupermarket.Infrastructure.Inventory;
using OnlineSupermarket.Infrastructure.Payments;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Tests.Payments;

public sealed class PaymentCallbackProcessorTests
{
    private const string Provider = "VNPay";
    private const string EventId = "txn-001";

    private sealed class DbFixture : IDisposable
    {
        public string Name { get; } = Guid.NewGuid().ToString();
        public AppDbContext Db { get; }
        public PaymentCallbackProcessor Processor { get; }

        public DbFixture()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Name)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Db = new AppDbContext(options);
            Processor = new PaymentCallbackProcessor(Db, new InventoryMutationService(Db, TimeProvider.System));
        }

        public void Dispose() => Db.Dispose();
    }

    private static PaymentCallbackVerificationResult Callback(Guid orderId, decimal amount, bool success)
        => new(true, EventId, orderId, amount, success, $"{{\"provider\":\"{Provider}\",\"eventId\":\"{EventId}\"}}", null);

    private static Order SeedOrder(
        AppDbContext db,
        OrderStatus status,
        Promotion? promotion = null,
        Guid? productId = null,
        BranchInventory? inventory = null)
    {
        var user = Domain.Identity.User.Create($"c_{Guid.NewGuid():N}@t.com", "hash", "Customer", null);
        var branch = new Branch("Processor Branch", "1 Test", "0900000000", 10m, 106m);
        db.Users.Add(user);
        db.Branches.Add(branch);

        var itemProductId = productId ?? Guid.NewGuid();
        var order = Order.Create(
            user.Id,
            branch.Id,
            "Delivery",
            "Thu",
            "0900000000",
            "Thu, 0900000000, address",
            null,
            [(itemProductId, "Product A", "SKU-A", 100_000m, 2, 200_000m)],
            subtotal: 200_000m,
            discountAmount: promotion is null ? 0m : 10_000m,
            shippingFee: 15_000m,
            totalAmount: 200_000m - 10_000m + 15_000m,
            promotionId: promotion?.Id,
            promotionCodeSnapshot: promotion?.Code);
        if (status != OrderStatus.Pending)
            order.SetStatus(status, $"seed {status}");
        db.Orders.Add(order);

        if (inventory is not null)
            db.BranchInventories.Add(inventory);
        if (promotion is not null)
            db.Promotions.Add(promotion);

        return order;
    }

    [Fact]
    public async Task Success_CompletesNewestPaymentAndConfirmsOrder()
    {
        using var fixture = new DbFixture();
        var order = SeedOrder(fixture.Db, OrderStatus.Pending);
        var total = order.TotalAmount;
        var older = Payment.Create(order.Id, PaymentMethod.VNPay, total);
        older.GetType().GetProperty(nameof(Payment.CreatedAtUtc))!.SetValue(older, DateTime.UtcNow.AddHours(-2));
        var newest = Payment.Create(order.Id, PaymentMethod.VNPay, total);
        fixture.Db.Payments.AddRange(older, newest);
        await fixture.Db.SaveChangesAsync();

        var outcome = await fixture.Processor.ProcessAsync(Provider, Callback(order.Id, total, true), CancellationToken.None);

        Assert.Equal(PaymentCallbackOutcome.Processed, outcome);
        var reloadedNewest = await fixture.Db.Payments.AsNoTracking().FirstAsync(p => p.Id == newest.Id);
        Assert.Equal(PaymentStatus.Completed, reloadedNewest.Status);
        Assert.Equal(EventId, reloadedNewest.ProviderTransactionId);
        var reloadedOlder = await fixture.Db.Payments.AsNoTracking().FirstAsync(p => p.Id == older.Id);
        Assert.Equal(PaymentStatus.Pending, reloadedOlder.Status);
        var reloadedOrder = await fixture.Db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Confirmed, reloadedOrder.Status);
    }

    [Fact]
    public async Task Success_WhenOrderAlreadyConfirmed_KeepsStateAndHistory()
    {
        using var fixture = new DbFixture();
        var order = SeedOrder(fixture.Db, OrderStatus.Confirmed);
        var total = order.TotalAmount;
        var payment = Payment.Create(order.Id, PaymentMethod.VNPay, total);
        fixture.Db.Payments.Add(payment);
        await fixture.Db.SaveChangesAsync();

        var outcome = await fixture.Processor.ProcessAsync(Provider, Callback(order.Id, total, true), CancellationToken.None);

        Assert.Equal(PaymentCallbackOutcome.Processed, outcome);
        var reloadedOrder = await fixture.Db.Orders.Include(o => o.StatusHistory).AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Confirmed, reloadedOrder.Status);
        Assert.Equal(2, reloadedOrder.StatusHistory.Count);
    }

    [Fact]
    public async Task Duplicate_Callback_ReturnsAlreadyProcessed_WithoutEffects()
    {
        using var fixture = new DbFixture();
        var order = SeedOrder(fixture.Db, OrderStatus.Confirmed);
        var total = order.TotalAmount;
        var payment = Payment.Create(order.Id, PaymentMethod.VNPay, total);
        fixture.Db.Payments.Add(payment);
        fixture.Db.PaymentCallbacks.Add(PaymentCallback.Create(payment.Id, Provider, EventId, "{}", true, total, PaymentStatus.Completed));
        await fixture.Db.SaveChangesAsync();

        var outcome = await fixture.Processor.ProcessAsync(Provider, Callback(order.Id, total, true), CancellationToken.None);

        Assert.Equal(PaymentCallbackOutcome.AlreadyProcessed, outcome);
        Assert.Equal(1, await fixture.Db.PaymentCallbacks.CountAsync());
        var reloaded = await fixture.Db.Payments.AsNoTracking().SingleAsync(p => p.Id == payment.Id);
        Assert.Equal(PaymentStatus.Pending, reloaded.Status);
    }

    [Fact]
    public async Task ProviderMismatch_ReturnsConflict()
    {
        using var fixture = new DbFixture();
        var order = SeedOrder(fixture.Db, OrderStatus.Confirmed);
        var payment = Payment.Create(order.Id, PaymentMethod.VNPay, order.TotalAmount);
        fixture.Db.Payments.Add(payment);
        await fixture.Db.SaveChangesAsync();

        var outcome = await fixture.Processor.ProcessAsync("MoMo", Callback(order.Id, order.TotalAmount, true), CancellationToken.None);

        Assert.Equal(PaymentCallbackOutcome.Conflict, outcome);
    }

    [Fact]
    public async Task AmountMismatch_ReturnsConflict()
    {
        using var fixture = new DbFixture();
        var order = SeedOrder(fixture.Db, OrderStatus.Confirmed);
        var payment = Payment.Create(order.Id, PaymentMethod.VNPay, order.TotalAmount);
        fixture.Db.Payments.Add(payment);
        await fixture.Db.SaveChangesAsync();

        var outcome = await fixture.Processor.ProcessAsync(Provider, Callback(order.Id, order.TotalAmount + 1, true), CancellationToken.None);

        Assert.Equal(PaymentCallbackOutcome.Conflict, outcome);
    }

    [Fact]
    public async Task TerminalPayment_ReturnsConflict()
    {
        using var fixture = new DbFixture();
        var order = SeedOrder(fixture.Db, OrderStatus.Confirmed);
        var payment = Payment.Create(order.Id, PaymentMethod.VNPay, order.TotalAmount);
        payment.MarkCompleted("old-txn");
        fixture.Db.Payments.Add(payment);
        await fixture.Db.SaveChangesAsync();

        var outcome = await fixture.Processor.ProcessAsync(Provider, Callback(order.Id, order.TotalAmount, true), CancellationToken.None);

        Assert.Equal(PaymentCallbackOutcome.Conflict, outcome);
    }

    [Fact]
    public async Task CancelledOrder_ReturnsConflict()
    {
        using var fixture = new DbFixture();
        var order = SeedOrder(fixture.Db, OrderStatus.Cancelled);
        var payment = Payment.Create(order.Id, PaymentMethod.VNPay, order.TotalAmount);
        fixture.Db.Payments.Add(payment);
        await fixture.Db.SaveChangesAsync();

        var outcome = await fixture.Processor.ProcessAsync(Provider, Callback(order.Id, order.TotalAmount, true), CancellationToken.None);

        Assert.Equal(PaymentCallbackOutcome.Conflict, outcome);
    }

    [Fact]
    public async Task NoPayment_ReturnsPaymentNotFound()
    {
        using var fixture = new DbFixture();
        var order = SeedOrder(fixture.Db, OrderStatus.Pending);

        var outcome = await fixture.Processor.ProcessAsync(Provider, Callback(order.Id, order.TotalAmount, true), CancellationToken.None);

        Assert.Equal(PaymentCallbackOutcome.PaymentNotFound, outcome);
    }

    [Fact]
    public async Task Failure_ReleasesInventoryOnce_ReleasesPromotionUsageAndCancelsOrder()
    {
        using var fixture = new DbFixture();
        var promotion = Promotion.Create("SUMMER10", DiscountType.Percentage, 10, 0, usageLimit: 50);
        promotion.IncrementUsage();
        var order = SeedOrder(fixture.Db, OrderStatus.Confirmed, promotion);
        var item = order.Items.Single();
        var inventory = BranchInventory.Create(order.BranchId, item.ProductId, 100_000m, 100, 5);
        inventory.Reserve(2);
        fixture.Db.BranchInventories.Add(inventory);
        var total = order.TotalAmount;
        var payment = Payment.Create(order.Id, PaymentMethod.VNPay, total);
        fixture.Db.Payments.Add(payment);
        await fixture.Db.SaveChangesAsync();

        var outcome = await fixture.Processor.ProcessAsync(Provider, Callback(order.Id, total, false), CancellationToken.None);

        Assert.Equal(PaymentCallbackOutcome.Processed, outcome);
        var release = Assert.Single(await fixture.Db.InventoryTransactions.AsNoTracking().ToListAsync());
        Assert.Equal(InventoryTransactionType.Release, release.TransactionType);
        Assert.Equal(-2, release.ReservedQuantityDelta);
        Assert.Equal($"order:{order.Id}:inventory:{inventory.Id}:release", release.OperationKey);
        var reloadedInventory = await fixture.Db.BranchInventories.AsNoTracking().SingleAsync(bi => bi.Id == inventory.Id);
        Assert.Equal(0, reloadedInventory.ReservedQuantity);
        var reloadedPromotion = await fixture.Db.Promotions.AsNoTracking().SingleAsync(p => p.Id == promotion.Id);
        Assert.Equal(0, reloadedPromotion.UsageCount);
        var reloadedOrder = await fixture.Db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Cancelled, reloadedOrder.Status);
        var reloadedPayment = await fixture.Db.Payments.AsNoTracking().SingleAsync(p => p.Id == payment.Id);
        Assert.Equal(PaymentStatus.Failed, reloadedPayment.Status);
        Assert.Single(await fixture.Db.PaymentCallbacks.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Failure_MissingInventory_RollsBackOrderAndReleasesNothing()
    {
        using var fixture = new DbFixture();
        var order = SeedOrder(fixture.Db, OrderStatus.Confirmed, productId: Guid.NewGuid());
        var payment = Payment.Create(order.Id, PaymentMethod.VNPay, order.TotalAmount);
        fixture.Db.Payments.Add(payment);
        await fixture.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Processor.ProcessAsync(Provider, Callback(order.Id, order.TotalAmount, false), CancellationToken.None));

        var reloadedOrder = await fixture.Db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Confirmed, reloadedOrder.Status);
        var reloadedPayment = await fixture.Db.Payments.AsNoTracking().SingleAsync(p => p.Id == payment.Id);
        Assert.Equal(PaymentStatus.Pending, reloadedPayment.Status);
        Assert.Equal(0, await fixture.Db.InventoryTransactions.CountAsync());
        Assert.Equal(0, await fixture.Db.PaymentCallbacks.CountAsync());
    }
}