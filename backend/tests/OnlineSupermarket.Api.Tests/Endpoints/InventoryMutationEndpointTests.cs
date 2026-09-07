using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Api.Contracts.Checkout;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Payments;
using OnlineSupermarket.Domain.Shopping;
using OnlineSupermarket.Infrastructure.Identity;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests.Endpoints;

public sealed class InventoryMutationEndpointTests
{
    private sealed record CheckoutSeed(HttpClient Client, Guid UserId, Guid InventoryId);
    private sealed record OrderSeed(HttpClient AdminClient, Guid OrderId, Guid InventoryId);

    private static async Task<CheckoutSeed> SeedCheckoutAsync(
        TestApiFactory factory,
        int inventoryQty,
        int cartQty)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = User.Create($"customer_{Guid.NewGuid():N}@test.com", "hash", "Customer", null);
        var branch = new Branch("Ledger Branch", "1 Test Street", "0900000000", 10m, 106m);
        var productId = Guid.NewGuid();
        var inventory = BranchInventory.Create(branch.Id, productId, 100_000m, inventoryQty, 5);
        var cart = new Cart(user.Id, branch.Id);
        cart.AddItem(productId, inventory.Id, 100_000m, cartQty);

        db.Users.Add(user);
        db.Branches.Add(branch);
        db.BranchInventories.Add(inventory);
        db.Carts.Add(cart);
        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(user));

        return new CheckoutSeed(client, user.Id, inventory.Id);
    }

    [Fact]
    public async Task Checkout_LogsReserveTransactionWithOrderReference()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedCheckoutAsync(factory, inventoryQty: 100, cartQty: 2);

        var response = await seed.Client.PostAsJsonAsync("/api/checkout", new CheckoutRequest("Pickup"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
        Assert.NotNull(body);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ledger = await db.InventoryTransactions.SingleAsync();
        Assert.Equal(InventoryTransactionType.Reserve, ledger.TransactionType);
        Assert.Equal(0, ledger.QuantityOnHandDelta);
        Assert.Equal(2, ledger.ReservedQuantityDelta);
        Assert.Equal(100, ledger.QuantityOnHandAfter);
        Assert.Equal(2, ledger.ReservedQuantityAfter);
        Assert.Equal(InventoryReferenceType.Order, ledger.ReferenceType);
        Assert.Equal(body.OrderId, ledger.ReferenceId);
        Assert.Equal(
            $"order:{body.OrderId}:inventory:{seed.InventoryId}:reserve",
            ledger.OperationKey);
    }

    [Fact]
    public async Task Checkout_WithInsufficientStock_RollsBackOrderAndLedger()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedCheckoutAsync(factory, inventoryQty: 2, cartQty: 5);

        var response = await seed.Client.PostAsJsonAsync("/api/checkout", new CheckoutRequest("Pickup"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(await db.Orders.AnyAsync(o => o.UserId == seed.UserId));
        Assert.Equal(0, await db.InventoryTransactions.CountAsync());

        var inventory = await db.BranchInventories.SingleAsync(bi => bi.Id == seed.InventoryId);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(2, inventory.QuantityOnHand);
    }

    private static async Task<HttpClient> CreateAdminClientAsync(TestApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var admin = User.Create($"admin_{Guid.NewGuid():N}@test.com", "hash", "Admin", null, UserRole.Admin);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(admin));
        return client;
    }

    private static async Task<OrderSeed> SeedReservedOrderAsync(
        TestApiFactory factory,
        int quantity,
        OrderStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = User.Create($"customer_order_{Guid.NewGuid():N}@test.com", "hash", "Customer", null);
        var branch = new Branch("Ledger Order Branch", "1 Test Street", "0900000000", 10m, 106m);
        var productId = Guid.NewGuid();
        var inventory = BranchInventory.Create(branch.Id, productId, 100_000m, 100, 5);
        inventory.Reserve(quantity);

        var order = Order.Create(
            customer.Id,
            branch.Id,
            "Delivery",
            "Thu Ban",
            "0900000000",
            "Thu, 0900000000, 1 Test Street",
            null,
            [(productId, "Product A", "SKU-A", 100_000m, quantity, 100_000m * quantity)],
            subtotal: 100_000m * quantity,
            discountAmount: 0m,
            shippingFee: 15_000m,
            totalAmount: 100_000m * quantity + 15_000m);

        var path = new[]
        {
            OrderStatus.Pending,
            OrderStatus.Confirmed,
            OrderStatus.Preparing,
            OrderStatus.Ready,
            OrderStatus.Shipped,
            OrderStatus.Delivered,
        };
        var targetIndex = Array.IndexOf(path, status);
        for (var i = 1; i <= targetIndex; i++)
        {
            order.SetStatus(path[i], $"transition to {path[i]}");
        }

        db.Users.Add(customer);
        db.Branches.Add(branch);
        db.BranchInventories.Add(inventory);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return new OrderSeed(await CreateAdminClientAsync(factory), order.Id, inventory.Id);
    }

    private static async Task<List<InventoryTransaction>> LoadTransactionsAsync(
        TestApiFactory factory,
        InventoryTransactionType type)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.InventoryTransactions
            .Where(t => t.TransactionType == type)
            .ToListAsync();
    }

    [Fact]
    public async Task CompletingOrder_ConvertsReservationToSaleExactlyOnce()
    {
        using var factory = new TestApiFactory();
        var fixture = await SeedReservedOrderAsync(factory, quantity: 3, OrderStatus.Delivered);

        var first = await fixture.AdminClient.PutAsJsonAsync(
            $"/api/admin/orders/{fixture.OrderId}/status",
            new { status = "Completed", note = "delivered to customer" });
        var second = await fixture.AdminClient.PutAsJsonAsync(
            $"/api/admin/orders/{fixture.OrderId}/status",
            new { status = "Completed" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        var sale = Assert.Single(await LoadTransactionsAsync(factory, InventoryTransactionType.Sale));
        Assert.Equal(-3, sale.QuantityOnHandDelta);
        Assert.Equal(-3, sale.ReservedQuantityDelta);
        Assert.Equal(fixture.InventoryId, sale.BranchInventoryId);
        Assert.Equal(fixture.OrderId, sale.ReferenceId);
        Assert.Equal(
            $"order:{fixture.OrderId}:inventory:{fixture.InventoryId}:sale",
            sale.OperationKey);
    }

    [Fact]
    public async Task CancellingOrder_LogsReleaseTransactionOnce()
    {
        using var factory = new TestApiFactory();
        var fixture = await SeedReservedOrderAsync(factory, quantity: 3, OrderStatus.Confirmed);

        var first = await fixture.AdminClient.PutAsJsonAsync(
            $"/api/admin/orders/{fixture.OrderId}/status",
            new { status = "Cancelled", note = "customer request" });
        var second = await fixture.AdminClient.PutAsJsonAsync(
            $"/api/admin/orders/{fixture.OrderId}/status",
            new { status = "Cancelled" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        var release = Assert.Single(await LoadTransactionsAsync(factory, InventoryTransactionType.Release));
        Assert.Equal(0, release.QuantityOnHandDelta);
        Assert.Equal(-3, release.ReservedQuantityDelta);
        Assert.Equal(0, release.ReservedQuantityAfter);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inventory = await db.BranchInventories.SingleAsync(bi => bi.Id == fixture.InventoryId);
        Assert.Equal(0, inventory.ReservedQuantity);
    }

    private static readonly System.Security.Cryptography.HMACSHA512 SignatureAlgorithm = new(
        System.Text.Encoding.UTF8.GetBytes(TestApiFactory.TestSecret));

    private static Dictionary<string, string> SignedVnPayCallback(Guid orderId, decimal amount, string eventId)
    {
        var data = new Dictionary<string, string>
        {
            ["vnp_Amount"] = ((long)(amount * 100)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["vnp_OrderInfo"] = "Payment",
            ["vnp_ResponseCode"] = "99",
            ["vnp_TransactionNo"] = eventId,
            ["vnp_TxnRef"] = orderId.ToString(),
        };
        var canonical = string.Join("&",
            data.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{Rfc3986(p.Key)}={Rfc3986(p.Value)}"));
        var digest = SignatureAlgorithm.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonical));
        data["vnp_SecureHash"] = Convert.ToHexString(digest).ToLowerInvariant();
        data["vnp_SecureHashType"] = "SHA512";
        return data;
    }

    private static string Rfc3986(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(value))
        {
            if ((b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z') || (b >= '0' && b <= '9')
                || b == '-' || b == '_' || b == '.' || b == '~')
                builder.Append((char)b);
            else
                builder.Append('%').Append(b.ToString("X2"));
        }
        return builder.ToString();
    }

    [Fact]
    public async Task PaymentFailure_ReleasesReservationOnce()
    {
        using var factory = new TestApiFactory();
        var fixture = await SeedReservedOrderAsync(factory, quantity: 2, OrderStatus.Confirmed);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.FirstAsync(o => o.Id == fixture.OrderId);
        var payment = Payment.Create(order.Id, PaymentMethod.VNPay, order.TotalAmount);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        for (var i = 0; i < 2; i++)
        {
            var response = await client.PostAsJsonAsync("/api/checkout/payment/callback", new
            {
                provider = "vnpay",
                data = SignedVnPayCallback(order.Id, order.TotalAmount, "txn-001"),
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var release = Assert.Single(await LoadTransactionsAsync(factory, InventoryTransactionType.Release));
        Assert.Equal(-2, release.ReservedQuantityDelta);
        Assert.Equal(fixture.InventoryId, release.BranchInventoryId);
        Assert.Equal(
            $"order:{fixture.OrderId}:inventory:{fixture.InventoryId}:release",
            release.OperationKey);

        var reloaded = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == fixture.OrderId);
        Assert.Equal(OrderStatus.Cancelled, reloaded.Status);

        var inventory = await db.BranchInventories.AsNoTracking()
            .SingleAsync(bi => bi.Id == fixture.InventoryId);
        Assert.Equal(0, inventory.ReservedQuantity);
    }

    [Fact]
    public async Task PaymentFailure_EndToEnd_ReleasesInventoryExactlyOnce()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedCheckoutAsync(factory, inventoryQty: 100, cartQty: 3);

        var checkout = await seed.Client.PostAsJsonAsync("/api/checkout", new CheckoutRequest("Pickup"));
        Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
        var checkoutBody = await checkout.Content.ReadFromJsonAsync<CheckoutResponse>();

        var payment = await seed.Client.PostAsJsonAsync("/api/checkout/payment",
            new PaymentRequest(checkoutBody!.OrderId, "VNPay"));
        Assert.Equal(HttpStatusCode.OK, payment.StatusCode);

        var client = factory.CreateClient();
        for (var i = 0; i < 2; i++)
        {
            var response = await client.PostAsJsonAsync("/api/checkout/payment/callback", new
            {
                provider = "vnpay",
                data = SignedVnPayCallback(checkoutBody.OrderId, checkoutBody.TotalAmount, "e2e-fail"),
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var release = Assert.Single(await LoadTransactionsAsync(factory, InventoryTransactionType.Release));
        Assert.Equal(-3, release.ReservedQuantityDelta);
        Assert.Equal(0, release.QuantityOnHandDelta);
        Assert.Equal(seed.InventoryId, release.BranchInventoryId);

        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == checkoutBody.OrderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);

        var inventory = await db.BranchInventories.AsNoTracking()
            .SingleAsync(bi => bi.Id == seed.InventoryId);
        Assert.Equal(100, inventory.QuantityOnHand);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(100, inventory.AvailableQuantity);
    }
}