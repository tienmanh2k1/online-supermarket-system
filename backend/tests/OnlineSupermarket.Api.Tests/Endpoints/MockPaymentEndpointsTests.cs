using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Api.Contracts.Checkout;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Payments;
using OnlineSupermarket.Infrastructure.Identity;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests.Endpoints;

public sealed class MockPaymentEndpointsTests
{
    [Fact]
    public async Task Owner_InitiatesMockOnlinePayment_UsingInternalUrl_WithoutConfirmingOrder()
    {
        using var factory = new TestApiFactory();
        var (client, order) = await SeedPendingOrderAsync(factory);

        var response = await client.PostAsJsonAsync("/api/checkout/payment", new PaymentRequest(order.Id, "MoMo"));
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paymentId = document.RootElement.GetProperty("paymentId").GetGuid();
        Assert.Equal($"/shopping/payment/mock/{paymentId}", document.RootElement.GetProperty("checkoutUrl").GetString());
        Assert.True(document.RootElement.GetProperty("isMock").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var payment = await db.Payments.SingleAsync(item => item.Id == paymentId);
        var persistedOrder = await db.Orders.SingleAsync(item => item.Id == order.Id);
        Assert.True(payment.IsMock);
        Assert.Equal(order.TotalAmount, payment.Amount);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(OrderStatus.Pending, persistedOrder.Status);
    }

    [Fact]
    public async Task RepeatedMockInitiation_ReturnsTheExistingPayment()
    {
        using var factory = new TestApiFactory();
        var (client, order) = await SeedPendingOrderAsync(factory);

        var first = await client.PostAsJsonAsync("/api/checkout/payment", new PaymentRequest(order.Id, "VNPay"));
        var second = await client.PostAsJsonAsync("/api/checkout/payment", new PaymentRequest(order.Id, "VNPay"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var secondBody = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(
            firstBody.RootElement.GetProperty("paymentId").GetGuid(),
            secondBody.RootElement.GetProperty("paymentId").GetGuid());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Payments.CountAsync(item => item.OrderId == order.Id));
    }

    [Fact]
    public async Task SandboxMode_ReportsOnlinePaymentsUnavailable_WithoutCreatingPayment()
    {
        using var factory = new TestApiFactory().WithWebHostBuilder(builder =>
            builder.UseSetting("Payments:Mode", "Sandbox"));
        var (client, order) = await SeedPendingOrderAsync(factory);

        var options = await client.GetAsync("/api/checkout/payment-options");
        var response = await client.PostAsJsonAsync("/api/checkout/payment", new PaymentRequest(order.Id, "VNPay"));

        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        var optionsBody = JsonDocument.Parse(await options.Content.ReadAsStringAsync());
        Assert.False(optionsBody.RootElement.GetProperty("onlineEnabled").GetBoolean());
        Assert.Equal("PAYMENT_PROVIDER_NOT_CONFIGURED", optionsBody.RootElement.GetProperty("disabledReason").GetString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Payments.Where(item => item.OrderId == order.Id).ToListAsync());
    }

    [Fact]
    public async Task Owner_CanCompleteMockPayment_AndReadPersistedSuccessState()
    {
        using var factory = new TestApiFactory();
        var (client, order) = await SeedPendingOrderAsync(factory);
        var initiated = await client.PostAsJsonAsync("/api/checkout/payment", new PaymentRequest(order.Id, "VNPay"));
        var initiatedBody = JsonDocument.Parse(await initiated.Content.ReadAsStringAsync());
        var paymentId = initiatedBody.RootElement.GetProperty("paymentId").GetGuid();

        var completed = await client.PostAsJsonAsync($"/api/payments/mock/{paymentId}/complete", new { outcome = "Success" });
        var detail = await client.GetAsync($"/api/payments/mock/{paymentId}");

        Assert.True(completed.StatusCode == HttpStatusCode.OK, await completed.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = JsonDocument.Parse(await detail.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Completed", body.GetProperty("paymentStatus").GetString());
        Assert.Equal("Confirmed", body.GetProperty("orderStatus").GetString());
        Assert.Equal("Success", body.GetProperty("outcome").GetString());
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Cancelled")]
    public async Task FailedOrCancelledMockPayment_IsIdempotent(string outcome)
    {
        using var factory = new TestApiFactory();
        var (client, order) = await SeedPendingOrderAsync(factory);
        var initiated = await client.PostAsJsonAsync("/api/checkout/payment", new PaymentRequest(order.Id, "MoMo"));
        var paymentId = JsonDocument.Parse(await initiated.Content.ReadAsStringAsync()).RootElement.GetProperty("paymentId").GetGuid();

        var first = await client.PostAsJsonAsync($"/api/payments/mock/{paymentId}/complete", new { outcome });
        var second = await client.PostAsJsonAsync($"/api/payments/mock/{paymentId}/complete", new { outcome });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.PaymentCallbacks.CountAsync(item => item.PaymentId == paymentId));
        Assert.Equal(PaymentStatus.Failed, (await db.Payments.SingleAsync(item => item.Id == paymentId)).Status);
        Assert.Equal(OrderStatus.Cancelled, (await db.Orders.SingleAsync(item => item.Id == order.Id)).Status);
        var detail = await client.GetAsync($"/api/payments/mock/{paymentId}");
        var detailBody = JsonDocument.Parse(await detail.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(outcome, detailBody.GetProperty("outcome").GetString());
    }

    private static async Task<(HttpClient Client, Order Order)> SeedPendingOrderAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var user = User.Create($"mock_{Guid.NewGuid():N}@test.com", "hash", "Mock Customer", null);
        var order = Order.Create(
            user.Id,
            Guid.NewGuid(),
            "Pickup",
            "Mock Customer",
            "0900000000",
            "Pickup branch",
            null,
            [],
            subtotal: 100_000m,
            discountAmount: 0m,
            shippingFee: 0m,
            totalAmount: 100_000m);
        db.Users.Add(user);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(user));
        return (client, order);
    }
}
