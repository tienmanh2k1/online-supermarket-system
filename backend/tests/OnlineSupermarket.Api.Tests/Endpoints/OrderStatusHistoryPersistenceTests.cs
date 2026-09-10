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
/// Verifies order status history is persisted correctly to DB after each transition.
/// Uses fresh DbContext scopes to ensure we're reading from DB, not in-memory objects.
/// </summary>
public sealed class OrderStatusHistoryPersistenceTests
{
    private static async Task<(HttpClient CustomerClient, HttpClient AdminClient, Guid OrderId)> SeedCodOrderAsync(
        TestApiFactory factory,
        string fulfillmentType = "Delivery")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        // Create branch with inventory
        var branch = new Branch("Test Branch", "123 Test St", "0900000001", 10.5m, 106.7m);
        db.Branches.Add(branch);

        // Create product
        var productId = Guid.NewGuid();
        var inventory = BranchInventory.Create(branch.Id, productId, 500_000m, 100, 10);
        db.BranchInventories.Add(inventory);

        // Create customer
        var user = User.Create($"customer_{Guid.NewGuid():N}@test.com", "hash", "Test Customer", null);
        db.Users.Add(user);

        // Create admin
        var admin = User.Create($"admin_{Guid.NewGuid():N}@test.com", "hash", "Test Admin", null, UserRole.Admin);
        db.Users.Add(admin);

        // Create cart
        var cart = new Cart(user.Id, branch.Id);
        cart.AddItem(productId, inventory.Id, 500_000m, 1);
        db.Carts.Add(cart);

        await db.SaveChangesAsync();

        // Get customer client
        var customerClient = factory.CreateClient();
        customerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(user));

        // Get admin client
        var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(admin));

        // Checkout COD - Delivery needs recipient info
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

        // Initiate COD payment to auto-confirm the order
        var paymentRequest = new PaymentRequest(checkoutBody!.OrderId, "COD");
        var paymentResponse = await customerClient.PostAsJsonAsync("/api/checkout/payment", paymentRequest);
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);

        return (customerClient, adminClient, checkoutBody.OrderId);
    }

    private static async Task<OrderDetailDto> GetOrderDetailAsAdmin(TestApiFactory factory, Guid orderId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders
            .Include(o => o.StatusHistory)
            .FirstAsync(o => o.Id == orderId);

        // Return as DTO similar to API response
        return new OrderDetailDto(
            order.Id, order.UserId, order.BranchId, order.FulfillmentType,
            order.RecipientName, order.RecipientPhone, order.DeliveryAddressSnapshot,
            order.Subtotal, order.DiscountAmount, order.ShippingFee, order.TotalAmount,
            order.PromotionCodeSnapshot, order.Status.ToString(),
            order.CreatedAtUtc, order.UpdatedAtUtc,
            order.Items.Select(i => new OrderItemDto(
                i.Id, i.ProductId, i.ProductName, i.Sku, i.UnitPrice, i.Quantity, i.LineTotal,
                false, null)).ToList(),
            order.StatusHistory.Select(h => new StatusHistoryDto(
                h.FromStatus.ToString(), h.ToStatus.ToString(), h.Note, h.CreatedAtUtc)).ToList(),
            null);
    }

    private static async Task<OrderDetailDto> GetOrderDetailAsCustomer(HttpClient client, Guid orderId)
    {
        var response = await client.GetAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderDetailDto>())!;
    }

    [Fact]
    public async Task CodOrder_AutoConfirmed_HasCorrectHistory()
    {
        using var factory = new TestApiFactory();
        var (_, _, orderId) = await SeedCodOrderAsync(factory);

        // Create fresh scope to read from DB
        var detail = await GetOrderDetailAsAdmin(factory, orderId);

        // COD auto-confirmed should have: Order created -> Pending -> Confirmed
        // Or just: Order created (Pending) -> Confirmed
        Assert.NotEmpty(detail.StatusHistory);

        // Check the first history entry
        var firstEntry = detail.StatusHistory[0];
        Assert.NotNull(firstEntry);

        // Order should have progressed from initial state
        var lastEntry = detail.StatusHistory[^1];
        Assert.Equal("Confirmed", lastEntry.ToStatus);
    }

    [Fact]
    public async Task CodDelivery_AllTransitions_PersistFullHistory()
    {
        using var factory = new TestApiFactory();
        var (_, adminClient, orderId) = await SeedCodOrderAsync(factory, "Delivery");

        // Get initial history after auto-confirm
        var detail1 = await GetOrderDetailAsAdmin(factory, orderId);
        var historyAfterConfirm = detail1.StatusHistory.Count;
        Assert.True(historyAfterConfirm >= 1, "Should have at least 1 history entry after auto-confirm");

        // Transition: Confirmed -> Preparing
        var r2 = await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Preparing", null));
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        // Fresh read after Preparing
        var detail2 = await GetOrderDetailAsAdmin(factory, orderId);
        Assert.Equal(historyAfterConfirm + 1, detail2.StatusHistory.Count);

        // Verify preparing entry
        var preparingEntry = detail2.StatusHistory[^1];
        Assert.Equal("Confirmed", preparingEntry.FromStatus);
        Assert.Equal("Preparing", preparingEntry.ToStatus);

        // Transition: Preparing -> Shipped
        var r3 = await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Shipped", null));
        Assert.Equal(HttpStatusCode.OK, r3.StatusCode);

        // Fresh read after Shipped
        var detail3 = await GetOrderDetailAsAdmin(factory, orderId);
        Assert.Equal(historyAfterConfirm + 2, detail3.StatusHistory.Count);

        // Transition: Shipped -> Delivered
        var r4 = await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Delivered", null));
        Assert.Equal(HttpStatusCode.OK, r4.StatusCode);

        // Fresh read after Delivered
        var detail4 = await GetOrderDetailAsAdmin(factory, orderId);
        Assert.Equal(historyAfterConfirm + 3, detail4.StatusHistory.Count);

        // Transition: Delivered -> Completed
        var r5 = await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Completed", null));
        Assert.Equal(HttpStatusCode.OK, r5.StatusCode);

        // Fresh read after Completed - THIS IS THE KEY TEST
        var detail5 = await GetOrderDetailAsAdmin(factory, orderId);

        // Should have ALL intermediate steps preserved
        Assert.Equal(historyAfterConfirm + 4, detail5.StatusHistory.Count);

        // Verify chronological order
        var statuses = detail5.StatusHistory.Select(h => h.ToStatus).ToList();
        Assert.Contains("Confirmed", statuses);
        Assert.Contains("Preparing", statuses);
        Assert.Contains("Shipped", statuses);
        Assert.Contains("Delivered", statuses);
        Assert.Contains("Completed", statuses);

        // Verify order of transitions
        var preparingIdx = statuses.IndexOf("Preparing");
        var shippedIdx = statuses.IndexOf("Shipped");
        var deliveredIdx = statuses.IndexOf("Delivered");
        var completedIdx = statuses.IndexOf("Completed");

        Assert.True(preparingIdx < shippedIdx, "Preparing should come before Shipped");
        Assert.True(shippedIdx < deliveredIdx, "Shipped should come before Delivered");
        Assert.True(deliveredIdx < completedIdx, "Delivered should come before Completed");
    }

    [Fact]
    public async Task Customer_CanSeeCompleteStatusHistory()
    {
        using var factory = new TestApiFactory();
        var (customerClient, adminClient, orderId) = await SeedCodOrderAsync(factory);

        // Admin completes the order through all transitions
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Preparing", null));
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Shipped", null));
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Delivered", null));
        await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Completed", null));

        // Customer reads order detail
        var customerDetail = await GetOrderDetailAsCustomer(customerClient, orderId);

        // Customer should see the full history, not just the latest entry
        Assert.True(customerDetail.StatusHistory.Count >= 4,
            $"Customer should see full history. Got {customerDetail.StatusHistory.Count} entries: " +
            string.Join(", ", customerDetail.StatusHistory.Select(h => $"{h.FromStatus}->{h.ToStatus}")));

        // Verify all status changes are visible to customer
        var toStatuses = customerDetail.StatusHistory.Select(h => h.ToStatus).ToList();
        Assert.Contains("Completed", toStatuses);
    }

    [Fact]
    public async Task CancelledOrder_HistoryPreservesCancellationEntry()
    {
        using var factory = new TestApiFactory();
        var (_, adminClient, orderId) = await SeedCodOrderAsync(factory);

        // Get history after auto-confirm
        var detail1 = await GetOrderDetailAsAdmin(factory, orderId);
        var countAfterConfirm = detail1.StatusHistory.Count;

        // Cancel the order
        var cancelResponse = await adminClient.PutAsJsonAsync($"/api/admin/orders/{orderId}/status",
            new UpdateOrderStatusRequest("Cancelled", "Customer requested cancellation"));
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        // Fresh read - should have cancellation entry
        var detail2 = await GetOrderDetailAsAdmin(factory, orderId);

        Assert.Equal(countAfterConfirm + 1, detail2.StatusHistory.Count);

        var cancelEntry = detail2.StatusHistory[^1];
        Assert.Equal("Cancelled", cancelEntry.ToStatus);
        Assert.Equal("Customer requested cancellation", cancelEntry.Note);
    }
}
