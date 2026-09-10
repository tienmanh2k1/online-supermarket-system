using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Api.Contracts.Checkout;
using OnlineSupermarket.Api.Contracts.Order;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Payments;
using OnlineSupermarket.Domain.Shopping;
using OnlineSupermarket.Infrastructure.Identity;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests.Endpoints;

/// <summary>
/// Verifies COD payment lifecycle: when order is completed, COD payment should be marked as Completed.
/// Business rule: COD order Completed = money collected = payment Completed.
/// </summary>
public sealed class CodPaymentLifecycleTests
{
    private static async Task<(HttpClient CustomerClient, HttpClient AdminClient, Guid OrderId)> SeedCodOrderAsync(
        TestApiFactory factory,
        string fulfillmentType = "Delivery")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var branch = new Branch("Test Branch", "123 Test St", "0900000001", 10.5m, 106.7m);
        db.Branches.Add(branch);

        var productId = Guid.NewGuid();
        var inventory = BranchInventory.Create(branch.Id, productId, 500_000m, 100, 10);
        db.BranchInventories.Add(inventory);

        var user = User.Create($"customer_{Guid.NewGuid():N}@test.com", "hash", "Test Customer", null);
        var admin = User.Create($"admin_{Guid.NewGuid():N}@test.com", "hash", "Test Admin", null, UserRole.Admin);
        db.Users.AddRange(user, admin);

        var cart = new Cart(user.Id, branch.Id);
        cart.AddItem(productId, inventory.Id, 500_000m, 1);
        db.Carts.Add(cart);

        await db.SaveChangesAsync();

        var customerClient = factory.CreateClient();
        customerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(user));

        var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(admin));

        CheckoutRequest checkoutRequest;
        if (fulfillmentType == "Delivery")
        {
            checkoutRequest = new CheckoutRequest("Delivery", null, "Test Recipient", "0900000001",
                "123 Test Address, Test Ward, Test District, HCMC", null);
        }
        else
        {
            checkoutRequest = new CheckoutRequest("Pickup");
        }

        var checkoutResponse = await customerClient.PostAsJsonAsync("/api/checkout", checkoutRequest);
        Assert.Equal(HttpStatusCode.Created, checkoutResponse.StatusCode);
        var checkoutBody = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutResponse>();

        // Initiate COD payment
        var paymentRequest = new PaymentRequest(checkoutBody!.OrderId, "COD");
        var paymentResponse = await customerClient.PostAsJsonAsync("/api/checkout/payment", paymentRequest);
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);

        return (customerClient, adminClient, checkoutBody.OrderId);
    }

    private static async Task<(PaymentStatus Status, string? ProviderTransactionId)> GetCodPaymentStatusAsync(
        TestApiFactory factory, Guid orderId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var payment = await db.Payments
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Method == PaymentMethod.COD);

        return (payment?.Status ?? PaymentStatus.Pending, payment?.ProviderTransactionId);
    }

    [Fact]
    public async Task CodDelivery_Completed_PaymentShouldBeMarkedCompleted()
    {
        using var factory = new TestApiFactory();
        var (_, adminClient, orderId) = await SeedCodOrderAsync(factory, "Delivery");

        // Verify initial payment status is PendingCollection
        var (initialStatus, _) = await GetCodPaymentStatusAsync(factory, orderId);
        Assert.Equal(PaymentStatus.PendingCollection, initialStatus);

        // Progress order through all statuses to Completed
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Preparing", null));
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Shipped", null));
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Delivered", null));

        var completeResponse = await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Completed", null));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        // Verify COD payment is now Completed
        var (finalStatus, txId) = await GetCodPaymentStatusAsync(factory, orderId);
        Assert.Equal(PaymentStatus.Completed, finalStatus);
        Assert.NotNull(txId);
        Assert.StartsWith("COD-", txId);
    }

    [Fact]
    public async Task CodPickup_Completed_PaymentShouldBeMarkedCompleted()
    {
        using var factory = new TestApiFactory();
        var (_, adminClient, orderId) = await SeedCodOrderAsync(factory, "Pickup");

        // Verify initial payment status
        var (initialStatus, _) = await GetCodPaymentStatusAsync(factory, orderId);
        Assert.Equal(PaymentStatus.PendingCollection, initialStatus);

        // Progress order to Completed (Pickup: Confirmed -> Preparing -> Ready -> Delivered -> Completed)
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Preparing", null));
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Ready", null));
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Delivered", null));

        var completeResponse = await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Completed", null));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        // Verify COD payment is now Completed
        var (finalStatus, txId) = await GetCodPaymentStatusAsync(factory, orderId);
        Assert.Equal(PaymentStatus.Completed, finalStatus);
        Assert.NotNull(txId);
        Assert.StartsWith("COD-", txId);
    }

    [Fact]
    public async Task CodOrder_CompletedTwice_PaymentNotDuplicated()
    {
        using var factory = new TestApiFactory();
        var (_, adminClient, orderId) = await SeedCodOrderAsync(factory, "Pickup");

        // Progress to Completed
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Preparing", null));
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Ready", null));
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Delivered", null));

        var complete1 = await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Completed", null));
        Assert.Equal(HttpStatusCode.OK, complete1.StatusCode);

        var (statusAfterFirst, txId1) = await GetCodPaymentStatusAsync(factory, orderId);
        Assert.Equal(PaymentStatus.Completed, statusAfterFirst);

        // Try to complete again - should be idempotent
        var complete2 = await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Completed", null));

        // Order is already Completed, so this should return BadRequest
        Assert.Equal(HttpStatusCode.BadRequest, complete2.StatusCode);

        // Payment should still be Completed with same txId
        var (statusAfterSecond, txId2) = await GetCodPaymentStatusAsync(factory, orderId);
        Assert.Equal(PaymentStatus.Completed, statusAfterSecond);
        Assert.Equal(txId1, txId2);
    }

    [Fact]
    public async Task VnpayPayment_NotAffectedByCodCompletionRule()
    {
        using var factory = new TestApiFactory();

        // Seed order without initiating payment first
        var (_, adminClient, orderId) = await SeedVnpayOrderAsync(factory);

        // Progress order to Completed
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Preparing", null));
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Ready", null));
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Delivered", null));

        var completeResponse = await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Completed", null));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        // VNPay payment should still be Pending (not marked Completed by order completion)
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var vnpayPayment = await db.Payments
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Method == PaymentMethod.VNPay);

        Assert.NotNull(vnpayPayment);
        Assert.Equal(PaymentStatus.Pending, vnpayPayment.Status);
    }

    [Fact]
    public async Task MomoPayment_NotAffectedByCodCompletionRule()
    {
        using var factory = new TestApiFactory();

        // Seed order without initiating payment first
        var (_, adminClient, orderId) = await SeedMomoOrderAsync(factory);

        // Progress order to Completed
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Preparing", null));
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Ready", null));
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Delivered", null));

        var completeResponse = await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Completed", null));
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        // MoMo payment should still be Pending
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var momoPayment = await db.Payments
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Method == PaymentMethod.MoMo);

        Assert.NotNull(momoPayment);
        Assert.Equal(PaymentStatus.Pending, momoPayment.Status);
    }

    private static async Task<(HttpClient CustomerClient, HttpClient AdminClient, Guid OrderId)> SeedVnpayOrderAsync(
        TestApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var branch = new Branch("Test Branch", "123 Test St", "0900000001", 10.5m, 106.7m);
        db.Branches.Add(branch);

        var productId = Guid.NewGuid();
        var inventory = BranchInventory.Create(branch.Id, productId, 500_000m, 100, 10);
        db.BranchInventories.Add(inventory);

        var user = User.Create($"customer_{Guid.NewGuid():N}@test.com", "hash", "Test Customer", null);
        var admin = User.Create($"admin_{Guid.NewGuid():N}@test.com", "hash", "Test Admin", null, UserRole.Admin);
        db.Users.AddRange(user, admin);

        var cart = new Cart(user.Id, branch.Id);
        cart.AddItem(productId, inventory.Id, 500_000m, 1);
        db.Carts.Add(cart);

        await db.SaveChangesAsync();

        var customerClient = factory.CreateClient();
        customerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(user));

        var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(admin));

        // Checkout with Pickup
        var checkoutRequest = new CheckoutRequest("Pickup");
        var checkoutResponse = await customerClient.PostAsJsonAsync("/api/checkout", checkoutRequest);
        Assert.Equal(HttpStatusCode.Created, checkoutResponse.StatusCode);
        var checkoutBody = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutResponse>();

        // Initiate VNPay payment (not COD)
        var paymentRequest = new PaymentRequest(checkoutBody!.OrderId, "VNPay");
        var paymentResponse = await customerClient.PostAsJsonAsync("/api/checkout/payment", paymentRequest);
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);

        return (customerClient, adminClient, checkoutBody.OrderId);
    }

    private static async Task<(HttpClient CustomerClient, HttpClient AdminClient, Guid OrderId)> SeedMomoOrderAsync(
        TestApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var branch = new Branch("Test Branch", "123 Test St", "0900000001", 10.5m, 106.7m);
        db.Branches.Add(branch);

        var productId = Guid.NewGuid();
        var inventory = BranchInventory.Create(branch.Id, productId, 500_000m, 100, 10);
        db.BranchInventories.Add(inventory);

        var user = User.Create($"customer_{Guid.NewGuid():N}@test.com", "hash", "Test Customer", null);
        var admin = User.Create($"admin_{Guid.NewGuid():N}@test.com", "hash", "Test Admin", null, UserRole.Admin);
        db.Users.AddRange(user, admin);

        var cart = new Cart(user.Id, branch.Id);
        cart.AddItem(productId, inventory.Id, 500_000m, 1);
        db.Carts.Add(cart);

        await db.SaveChangesAsync();

        var customerClient = factory.CreateClient();
        customerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(user));

        var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(admin));

        // Checkout with Pickup
        var checkoutRequest = new CheckoutRequest("Pickup");
        var checkoutResponse = await customerClient.PostAsJsonAsync("/api/checkout", checkoutRequest);
        Assert.Equal(HttpStatusCode.Created, checkoutResponse.StatusCode);
        var checkoutBody = await checkoutResponse.Content.ReadFromJsonAsync<CheckoutResponse>();

        // Initiate MoMo payment (not COD)
        var paymentRequest = new PaymentRequest(checkoutBody!.OrderId, "MoMo");
        var paymentResponse = await customerClient.PostAsJsonAsync("/api/checkout/payment", paymentRequest);
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);

        return (customerClient, adminClient, checkoutBody.OrderId);
    }
}
