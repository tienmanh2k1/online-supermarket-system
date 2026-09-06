using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Payments;
using OnlineSupermarket.Domain.Promotions;
using OnlineSupermarket.Infrastructure.Inventory;
using OnlineSupermarket.Infrastructure.Payments;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Tests.Persistence;

[Collection(MySqlInfrastructureCollection.Name)]
public sealed class MySqlPaymentCallbackTests(MySqlFixture fixture) : IAsyncLifetime
{
    private readonly MySqlFixture _fixture = fixture;
    private const string TestDatabase = "online_supermarket_payment_callback_tests";

    private DbContextOptions<AppDbContext> Options =>
        new DbContextOptionsBuilder<AppDbContext>().UseMySQL(_fixture.CreateDatabaseConnectionString(TestDatabase)).Options;

    private AppDbContext CreateContext() => new(Options);

    public async Task InitializeAsync()
    {
        await using var master = new MySqlConnection(_fixture.MasterConnectionString);
        await master.OpenAsync();

        await using (var drop = new MySqlCommand("DROP DATABASE IF EXISTS " + TestDatabase + ";", master))
            await drop.ExecuteNonQueryAsync();
        await using (var create = new MySqlCommand("CREATE DATABASE " + TestDatabase + " CHARACTER SET utf8mb4;", master))
            await create.ExecuteNonQueryAsync();

        await using var db = new AppDbContext(Options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static PaymentCallbackVerificationResult Callback(Guid orderId, decimal amount, bool success, string eventId)
        => new(true, eventId, orderId, amount, success, $"{{\"provider\":\"VNPay\",\"eventId\":\"{eventId}\"}}", null);

    private async Task<(Guid BranchId, Guid ProductId)> SeedCatalogAndBranchAsync(AppDbContext db)
    {
        var category = new Category("Callback Cat", "callback-cat");
        var brand = new Brand("Callback Brand", "callback-brand");
        db.Categories.Add(category);
        db.Brands.Add(brand);
        await db.SaveChangesAsync();

        var branch = new Branch("Callback Branch", "1 Test Street", "09 0000 0000", 10m, 106m);
        var product = new Product(category.Id, brand.Id, "SKU-CB", "Callback Product", "callback-product", "d", 100_000m, "cái", null);
        db.Branches.Add(branch);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (branch.Id, product.Id);
    }

    private async Task<Order> SeedOrderAsync(
        AppDbContext db,
        Guid userId,
        Guid branchId,
        Guid productId,
        OrderStatus status,
        Promotion? promotion = null)
    {
        var discount = promotion is null ? 0m : 10_000m;
        var order = Order.Create(
            userId, branchId, "Delivery", "Thu", "0900000000", "Thu, 0900000000, 1 Test", null,
            [(productId, "Callback Product", "SKU-CB", 100_000m, 2, 200_000m)],
            subtotal: 200_000m, discountAmount: discount, shippingFee: 15_000m,
            totalAmount: 200_000m - discount + 15_000m,
            promotionId: promotion?.Id, promotionCodeSnapshot: promotion?.Code);
        if (status != OrderStatus.Pending)
            order.SetStatus(status, $"seed {status}");
        db.Orders.Add(order);
        return order;
    }

    private async Task<(Guid UserId, Guid BranchId, Guid ProductId, Guid InventoryId, Order Order, Payment Payment)> SeedOrderWithPaymentAsync(
        AppDbContext db, OrderStatus orderStatus, bool reserveInventory, Promotion? promotion = null)
    {
        var user = User.Create($"cb_{Guid.NewGuid():N}@t.com", "hash", "Customer", null);
        db.Users.Add(user);
        var (branchId, productId) = await SeedCatalogAndBranchAsync(db);

        if (promotion is not null)
            db.Promotions.Add(promotion);

        var order = await SeedOrderAsync(db, user.Id, branchId, productId, orderStatus, promotion);
        var inventoryId = Guid.Empty;
        if (reserveInventory)
        {
            var inventory = BranchInventory.Create(branchId, productId, 100_000m, 100, 5);
            inventory.Reserve(2);
            db.BranchInventories.Add(inventory);
        }

        var payment = Payment.Create(order.Id, PaymentMethod.VNPay, order.TotalAmount);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        if (reserveInventory)
        {
            inventoryId = await db.BranchInventories.AsNoTracking()
                .Where(bi => bi.BranchId == branchId)
                .Select(bi => bi.Id)
                .SingleAsync();
        }

        return (user.Id, branchId, productId, inventoryId, order, payment);
    }

    private async Task<PaymentCallbackOutcome> ProcessAsync(Guid orderId, decimal amount, bool success, string eventId)
    {
        await using var ctx = CreateContext();
        var processor = new PaymentCallbackProcessor(ctx, new InventoryMutationService(ctx, TimeProvider.System));
        return await processor.ProcessAsync("VNPay", Callback(orderId, amount, success, eventId), CancellationToken.None);
    }

    [Fact]
    public async Task Sequential_Duplicate_Callback_IsIdempotent_OnMySql()
    {
        await using var db = CreateContext();
        var seed = await SeedOrderWithPaymentAsync(db, OrderStatus.Pending, reserveInventory: false);

        var first = await ProcessAsync(seed.Order.Id, seed.Payment.Amount, true, "seq-dup-1");
        var second = await ProcessAsync(seed.Order.Id, seed.Payment.Amount, true, "seq-dup-1");

        Assert.Equal(PaymentCallbackOutcome.Processed, first);
        Assert.Equal(PaymentCallbackOutcome.AlreadyProcessed, second);

        await using var verify = CreateContext();
        Assert.Equal(1, await verify.PaymentCallbacks.CountAsync(x => x.ExternalEventId == "seq-dup-1"));
        var payment = await verify.Payments.AsNoTracking().SingleAsync(p => p.Id == seed.Payment.Id);
        Assert.Equal(PaymentStatus.Completed, payment.Status);
    }

    [Fact]
    public async Task Duplicate_Race_RepeatedFixedCount_OneEffectAlways_OnMySql()
    {
        const int rounds = 5;
        for (var round = 0; round < rounds; round++)
        {
            try
            {
                await using var db = CreateContext();
                var seed = await SeedOrderWithPaymentAsync(db, OrderStatus.Pending, reserveInventory: false);
                var eventId = $"race-round-{round}";

                var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var ready = new SemaphoreSlim(0, 2);

                async Task<PaymentCallbackOutcome> RaceOnceAsync()
                {
                    await using var ctx = CreateContext();
                    var processor = new PaymentCallbackProcessor(ctx, new InventoryMutationService(ctx, TimeProvider.System));
                    processor.BeforePaymentLockTestHook = () =>
                    {
                        ready.Release();
                        return gate.Task;
                    };
                    return await processor.ProcessAsync("VNPay", Callback(seed.Order.Id, seed.Payment.Amount, true, eventId), CancellationToken.None);
                }

                var task1 = Task.Run(RaceOnceAsync);
                var task2 = Task.Run(RaceOnceAsync);

                await ready.WaitAsync();
                await ready.WaitAsync();
                gate.SetResult();
                var outcomes = await Task.WhenAll(task1, task2);

                Assert.Contains(PaymentCallbackOutcome.Processed, outcomes);
                Assert.Contains(PaymentCallbackOutcome.AlreadyProcessed, outcomes);

                await using var verify = CreateContext();
                Assert.Equal(1, await verify.PaymentCallbacks.CountAsync(x => x.ExternalEventId == eventId));
                var payment = await verify.Payments.AsNoTracking().SingleAsync(p => p.Id == seed.Payment.Id);
                Assert.Equal(PaymentStatus.Completed, payment.Status);
                var order = await verify.Orders.AsNoTracking().SingleAsync(o => o.Id == seed.Order.Id);
                Assert.Equal(OrderStatus.Confirmed, order.Status);
            }
            finally
            {
                // Drop the per-round database so an early failure cannot leak
                // rows into the next round of the same race gate.
                await using var master = new MySqlConnection(_fixture.MasterConnectionString);
                await master.OpenAsync();
                await using var drop = new MySqlCommand("DROP DATABASE IF EXISTS " + TestDatabase + ";", master);
                await drop.ExecuteNonQueryAsync();
                await using var create = new MySqlCommand("CREATE DATABASE " + TestDatabase + " CHARACTER SET utf8mb4;", master);
                await create.ExecuteNonQueryAsync();
                await using var db = new AppDbContext(Options);
                await db.Database.MigrateAsync();
            }
        }
    }

    [Fact]
    public async Task Forced_Race_Two_Callbacks_SameEventId_OneEffect_OnMySql()
    {
        await using var db = CreateContext();
        var seed = await SeedOrderWithPaymentAsync(db, OrderStatus.Pending, reserveInventory: false);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new SemaphoreSlim(0, 2);

        async Task<PaymentCallbackOutcome> RaceOnceAsync()
        {
            await using var ctx = CreateContext();
            var processor = new PaymentCallbackProcessor(ctx, new InventoryMutationService(ctx, TimeProvider.System));
            processor.BeforePaymentLockTestHook = () =>
            {
                ready.Release();
                return gate.Task;
            };
            return await processor.ProcessAsync("VNPay", Callback(seed.Order.Id, seed.Payment.Amount, true, "race-forced"), CancellationToken.None);
        }

        var task1 = Task.Run(RaceOnceAsync);
        var task2 = Task.Run(RaceOnceAsync);

        await ready.WaitAsync();
        await ready.WaitAsync();
        gate.SetResult();
        var outcomes = await Task.WhenAll(task1, task2);

        Assert.Contains(PaymentCallbackOutcome.Processed, outcomes);
        Assert.Contains(PaymentCallbackOutcome.AlreadyProcessed, outcomes);

        await using var verify = CreateContext();
        Assert.Equal(1, await verify.PaymentCallbacks.CountAsync(x => x.ExternalEventId == "race-forced"));
        var payment = await verify.Payments.AsNoTracking().SingleAsync(p => p.Id == seed.Payment.Id);
        Assert.Equal(PaymentStatus.Completed, payment.Status);
        var order = await verify.Orders.AsNoTracking().SingleAsync(o => o.Id == seed.Order.Id);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public async Task Forced_Race_Success_And_Failure_OneTerminal_OnMySql()
    {
        var promotion = Promotion.Create("MYRACE", DiscountType.Percentage, 10, 0, usageLimit: 50);
        await using var db = CreateContext();
        promotion.IncrementUsage();
        var seed = await SeedOrderWithPaymentAsync(db, OrderStatus.Confirmed, reserveInventory: true, promotion);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new SemaphoreSlim(0, 2);

        async Task<PaymentCallbackOutcome> RaceOnceAsync(bool success, string eventId)
        {
            await using var ctx = CreateContext();
            var processor = new PaymentCallbackProcessor(ctx, new InventoryMutationService(ctx, TimeProvider.System));
            processor.BeforePaymentLockTestHook = () =>
            {
                ready.Release();
                return gate.Task;
            };
            return await processor.ProcessAsync("VNPay", Callback(seed.Order.Id, seed.Payment.Amount, success, eventId), CancellationToken.None);
        }

        var task1 = Task.Run(() => RaceOnceAsync(true, "race-succ"));
        var task2 = Task.Run(() => RaceOnceAsync(false, "race-fail"));

        await ready.WaitAsync();
        await ready.WaitAsync();
        gate.SetResult();
        var outcomes = await Task.WhenAll(task1, task2);

        Assert.Contains(PaymentCallbackOutcome.Processed, outcomes);
        Assert.Contains(PaymentCallbackOutcome.Conflict, outcomes);

        await using var verify = CreateContext();
        var payment = await verify.Payments.AsNoTracking().SingleAsync(p => p.Id == seed.Payment.Id);
        var order = await verify.Orders.AsNoTracking().SingleAsync(o => o.Id == seed.Order.Id);
        Assert.True(
            (payment.Status == PaymentStatus.Completed && order.Status == OrderStatus.Confirmed) ||
            (payment.Status == PaymentStatus.Failed && order.Status == OrderStatus.Cancelled),
            $"Inconsistent terminal state: payment={payment.Status} order={order.Status}");
        var releases = await verify.InventoryTransactions.AsNoTracking().CountAsync(t => t.TransactionType == InventoryTransactionType.Release);
        if (payment.Status == PaymentStatus.Failed)
        {
            Assert.Equal(1, releases);
            Assert.Equal(0, (await verify.Promotions.AsNoTracking().SingleAsync(p => p.Id == promotion.Id)).UsageCount);
        }
        else
        {
            Assert.Equal(0, releases);
        }
    }

    [Fact]
    public async Task Racing_Callbacks_EffectsExactlyOnce_OnMySql()
    {
        await using var db = CreateContext();
        var seed = await SeedOrderWithPaymentAsync(db, OrderStatus.Pending, reserveInventory: false);

        var task1 = ProcessAsync(seed.Order.Id, seed.Payment.Amount, true, "race-1");
        var task2 = ProcessAsync(seed.Order.Id, seed.Payment.Amount, true, "race-1");

        var outcomes = await Task.WhenAll(task1, task2);

        Assert.Contains(PaymentCallbackOutcome.Processed, outcomes);
        Assert.Contains(PaymentCallbackOutcome.AlreadyProcessed, outcomes);

        await using var verify = CreateContext();
        Assert.Equal(1, await verify.PaymentCallbacks.CountAsync(x => x.Provider == "VNPay" && x.ExternalEventId == "race-1"));
        var payment = await verify.Payments.AsNoTracking().SingleAsync(p => p.Id == seed.Payment.Id);
        Assert.Equal(PaymentStatus.Completed, payment.Status);
        var order = await verify.Orders.AsNoTracking().SingleAsync(o => o.Id == seed.Order.Id);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public async Task Latest_Payment_IsSelectedByCreationTime()
    {
        await using var db = CreateContext();
        var seed = await SeedOrderWithPaymentAsync(db, OrderStatus.Pending, reserveInventory: false);

        var older = Payment.Create(seed.Order.Id, PaymentMethod.VNPay, seed.Payment.Amount);
        older.GetType().GetProperty(nameof(Payment.CreatedAtUtc))!.SetValue(older, DateTime.UtcNow.AddHours(-2));
        var newer = Payment.Create(seed.Order.Id, PaymentMethod.VNPay, seed.Payment.Amount);
        db.Payments.AddRange(older, newer);
        await db.SaveChangesAsync();

        var outcome = await ProcessAsync(seed.Order.Id, seed.Payment.Amount, true, "latest-1");

        Assert.Equal(PaymentCallbackOutcome.Processed, outcome);
        await using var verify = CreateContext();
        Assert.Equal(PaymentStatus.Completed, (await verify.Payments.AsNoTracking().SingleAsync(p => p.Id == newer.Id)).Status);
        Assert.Equal(PaymentStatus.Pending, (await verify.Payments.AsNoTracking().SingleAsync(p => p.Id == older.Id)).Status);
    }

    [Fact]
    public async Task MethodMismatch_ReturnsConflict()
    {
        await using var db = CreateContext();
        var seed = await SeedOrderWithPaymentAsync(db, OrderStatus.Pending, reserveInventory: false);

        await using var ctx = CreateContext();
        var processor = new PaymentCallbackProcessor(ctx, new InventoryMutationService(ctx, TimeProvider.System));
        var outcome = await processor.ProcessAsync("MoMo", Callback(seed.Order.Id, seed.Payment.Amount, true, "momo-1"), CancellationToken.None);

        Assert.Equal(PaymentCallbackOutcome.Conflict, outcome);
    }

    [Fact]
    public async Task TerminalPayment_ReturnsConflict()
    {
        await using var db = CreateContext();
        var seed = await SeedOrderWithPaymentAsync(db, OrderStatus.Pending, reserveInventory: false);
        var payment = await db.Payments.SingleAsync(p => p.Id == seed.Payment.Id);
        payment.MarkCompleted("already-done");
        await db.SaveChangesAsync();

        var outcome = await ProcessAsync(seed.Order.Id, seed.Payment.Amount, true, "after-terminal");

        Assert.Equal(PaymentCallbackOutcome.Conflict, outcome);
    }

    [Fact]
    public async Task Failure_ReleasesInventoryAndPromotionOnce_AndRetryIsIdempotent()
    {
        var promotion = Promotion.Create("CB10", DiscountType.Percentage, 10, 0, usageLimit: 50);
        await using var db = CreateContext();
        promotion.IncrementUsage();
        var seed = await SeedOrderWithPaymentAsync(db, OrderStatus.Confirmed, reserveInventory: true, promotion);

        var first = await ProcessAsync(seed.Order.Id, seed.Payment.Amount, false, "fail-1");
        Assert.Equal(PaymentCallbackOutcome.Processed, first);

        await using var verify = CreateContext();
        var release = Assert.Single(await verify.InventoryTransactions.AsNoTracking().Where(t => t.TransactionType == InventoryTransactionType.Release).ToListAsync());
        Assert.Equal(-2, release.ReservedQuantityDelta);
        Assert.Equal(seed.InventoryId, release.BranchInventoryId);
        Assert.Equal($"order:{seed.Order.Id}:inventory:{seed.InventoryId}:release", release.OperationKey);
        var promotionAfter = await verify.Promotions.AsNoTracking().SingleAsync(p => p.Code == "CB10");
        Assert.Equal(0, promotionAfter.UsageCount);
        var order = await verify.Orders.AsNoTracking().SingleAsync(o => o.Id == seed.Order.Id);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        var payment = await verify.Payments.AsNoTracking().SingleAsync(p => p.Id == seed.Payment.Id);
        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Single(await verify.PaymentCallbacks.AsNoTracking().Where(c => c.Provider == "VNPay" && c.ExternalEventId == "fail-1").ToListAsync());

        var retry = await ProcessAsync(seed.Order.Id, seed.Payment.Amount, false, "fail-1");
        Assert.Equal(PaymentCallbackOutcome.AlreadyProcessed, retry);
        await using var verifyAfter = CreateContext();
        Assert.Equal(1, await verifyAfter.InventoryTransactions.CountAsync(t => t.TransactionType == InventoryTransactionType.Release));
    Assert.Equal(0, (await verifyAfter.Promotions.AsNoTracking().SingleAsync(p => p.Code == "CB10")).UsageCount);
    }

    [Fact]
    public async Task MissingInventory_Failure_Callback_RollsBackEverything()
    {
        await using var db = CreateContext();
        var seed = await SeedOrderWithPaymentAsync(db, OrderStatus.Confirmed, reserveInventory: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProcessAsync(seed.Order.Id, seed.Payment.Amount, false, "missing-inv-1"));

        await using var verify = CreateContext();
        var order = await verify.Orders.AsNoTracking().SingleAsync(o => o.Id == seed.Order.Id);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        var payment = await verify.Payments.AsNoTracking().SingleAsync(p => p.Id == seed.Payment.Id);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(0, await verify.InventoryTransactions.CountAsync());
        Assert.Equal(0, await verify.PaymentCallbacks.CountAsync());
    }

    [Fact]
    public async Task UniqueIndex_EnforcesProviderAndExternalEventId()
    {
        await using var db = CreateContext();
        var seed = await SeedOrderWithPaymentAsync(db, OrderStatus.Pending, reserveInventory: false);

        var indexCount = await db.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS Value FROM information_schema.STATISTICS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'payment_callbacks' " +
            "AND INDEX_NAME = 'ix_payment_callbacks_provider_event' " +
            "AND NON_UNIQUE = 0").SingleAsync();
        Assert.True(indexCount >= 2);
    }
}