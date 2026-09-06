using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Infrastructure.Payments;

namespace OnlineSupermarket.Api.Tests.Endpoints;

public sealed class PaymentCallbackEndpointsTests
{
    private static readonly Guid OrderGuid = Guid.Parse("9b0e6ef8-6d4c-4f85-9a3d-1e2f3c4b5a6d");

    private static PaymentCallbackVerificationResult Valid(string eventId = "VNPAY123456", bool success = true, decimal amount = 10000m)
        => new(true, eventId, OrderGuid, amount, success, $"{{\"provider\":\"VNPay\",\"eventId\":\"{eventId}\"}}", null);

    private sealed class StubVerifier : IPaymentCallbackVerifier
    {
        public string Provider { get; set; } = "VNPay";
        public PaymentCallbackVerificationResult Result { get; set; } = Valid();

        public PaymentCallbackVerificationResult Verify(IReadOnlyDictionary<string, string> data) => Result;
    }

    private sealed class StubProcessor : IPaymentCallbackProcessor
    {
        public PaymentCallbackOutcome Outcome { get; set; } = PaymentCallbackOutcome.Processed;
        public int Calls { get; private set; }
        public string? ReceivedProvider { get; private set; }

        public Task<PaymentCallbackOutcome> ProcessAsync(
            string provider,
            PaymentCallbackVerificationResult callback,
            CancellationToken cancellationToken)
        {
            Calls++;
            ReceivedProvider = provider;
            return Task.FromResult(Outcome);
        }
    }

    private sealed class CallbackTestFactory : TestApiFactory
    {
        public StubVerifier Verifier { get; } = new();
        public StubProcessor Processor { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                var verifierDescriptors = services
                    .Where(d => d.ServiceType == typeof(IPaymentCallbackVerifier))
                    .ToList();
                foreach (var descriptor in verifierDescriptors)
                {
                    services.Remove(descriptor);
                }
                services.AddScoped<IPaymentCallbackVerifier>(_ => Verifier);

                var processorDescriptor = services.Single(d => d.ServiceType == typeof(IPaymentCallbackProcessor));
                services.Remove(processorDescriptor);
                services.AddScoped<IPaymentCallbackProcessor>(_ => Processor);
            });
        }
    }

    private static Task<HttpResponseMessage> PostCallbackAsync(
        CallbackTestFactory factory,
        object? body) =>
        factory.CreateClient().PostAsJsonAsync("/api/checkout/payment/callback", body);

    [Fact]
    public async Task Unknown_Provider_Returns401_AndDoesNotCallProcessor()
    {
        using var factory = new CallbackTestFactory();

        var response = await PostCallbackAsync(factory, new { provider = "AMEX", data = new Dictionary<string, string> { ["vnp_TxnRef"] = OrderGuid.ToString() } });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.Processor.Calls);
    }

    [Fact]
    public async Task Malformed_Callback_Returns400_BeforeSignatureBranch()
    {
        using var factory = new CallbackTestFactory();
        factory.Verifier.Result = new(false, "evt", Guid.Empty, 0m, false, "{}", "MALFORMED_CALLBACK");

        var response = await PostCallbackAsync(factory, new { provider = "VNPay", data = new { amount = "x" } });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("MALFORMED_CALLBACK", body);
        Assert.Equal(0, factory.Processor.Calls);
    }

    [Fact]
    public async Task Invalid_Signature_Returns401()
    {
        using var factory = new CallbackTestFactory();
        factory.Verifier.Result = new(false, "evt", OrderGuid, 100m, true, "{}", "INVALID_SIGNATURE");

        var response = await PostCallbackAsync(factory, new { provider = "VNPay", data = new Dictionary<string, string> { ["vnp_TxnRef"] = OrderGuid.ToString() } });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.Processor.Calls);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task Missing_Or_Empty_Data_Returns400(string dataJson)
    {
        using var factory = new CallbackTestFactory();
        var body = $"{{\"provider\":\"VNPay\",\"data\":{dataJson}}}";
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        var response = await factory.CreateClient().PostAsync("/api/checkout/payment/callback", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Processor.Calls);
    }

    [Fact]
    public async Task Configuration_Failure_ReturnsSafe500()
    {
        using var factory = new CallbackTestFactory();
        factory.Verifier.Result = new(false, "evt", OrderGuid, 100m, true, "{}", "WEBHOOK_NOT_CONFIGURED");

        var response = await PostCallbackAsync(factory, new { provider = "VNPay", data = new Dictionary<string, string> { ["vnp_TxnRef"] = OrderGuid.ToString() } });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("WEBHOOK_NOT_CONFIGURED", body);
        Assert.Equal(0, factory.Processor.Calls);
    }

    [Fact]
    public async Task Verified_Processed_Returns200_AndPassesCanonicalProvider()
    {
        using var factory = new CallbackTestFactory();
        factory.Processor.Outcome = PaymentCallbackOutcome.Processed;

        var response = await PostCallbackAsync(factory, new
        {
            provider = "vnpay",
            data = new Dictionary<string, string> { ["vnp_TxnRef"] = OrderGuid.ToString() }
        });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("processed", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, factory.Processor.Calls);
        Assert.Equal("VNPay", factory.Processor.ReceivedProvider);
    }

    [Fact]
    public async Task AlreadyProcessed_Returns200_NoRepeatedEffect()
    {
        using var factory = new CallbackTestFactory();
        factory.Processor.Outcome = PaymentCallbackOutcome.AlreadyProcessed;

        var response = await PostCallbackAsync(factory, new { provider = "VNPay", data = new Dictionary<string, string> { ["vnp_TxnRef"] = OrderGuid.ToString() } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.Processor.Calls);
    }

    [Fact]
    public async Task PaymentNotFound_Returns404()
    {
        using var factory = new CallbackTestFactory();
        factory.Processor.Outcome = PaymentCallbackOutcome.PaymentNotFound;

        var response = await PostCallbackAsync(factory, new { provider = "VNPay", data = new Dictionary<string, string> { ["vnp_TxnRef"] = OrderGuid.ToString() } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, factory.Processor.Calls);
    }

    [Fact]
    public async Task Conflict_Returns409()
    {
        using var factory = new CallbackTestFactory();
        factory.Processor.Outcome = PaymentCallbackOutcome.Conflict;

        var response = await PostCallbackAsync(factory, new { provider = "VNPay", data = new Dictionary<string, string> { ["vnp_TxnRef"] = OrderGuid.ToString() } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Verified_But_EmptyOrderId_Returns400_BeforeDelegation()
    {
        using var factory = new CallbackTestFactory();
        factory.Verifier.Result = new(true, "evt-1", Guid.Empty, 100m, true, "{}", null);

        var response = await PostCallbackAsync(factory, new { provider = "VNPay", data = new Dictionary<string, string> { ["vnp_TxnRef"] = OrderGuid.ToString() } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Processor.Calls);
    }

    [Fact]
    public async Task Verified_But_NegativeAmount_Returns400_BeforeDelegation()
    {
        using var factory = new CallbackTestFactory();
        factory.Verifier.Result = new(true, "evt-1", OrderGuid, -1m, true, "{}", null);

        var response = await PostCallbackAsync(factory, new { provider = "VNPay", data = new Dictionary<string, string> { ["vnp_TxnRef"] = OrderGuid.ToString() } });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("MALFORMED_CALLBACK", body);
        Assert.Equal(0, factory.Processor.Calls);
    }

    [Fact]
    public async Task Response_DoesNotLeakSignatureOrErrorDetail()
    {
        using var factory = new CallbackTestFactory();
        factory.Processor.Outcome = PaymentCallbackOutcome.Conflict;

        var response = await PostCallbackAsync(factory, new
        {
            provider = "VNPay",
            data = new Dictionary<string, string>
            {
                ["vnp_SecureHash"] = "topsecret-signature",
                ["vnp_Note"] = TestApiFactory.TestSecret
            }
        });
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("topsecret-signature", body);
        Assert.DoesNotContain(TestApiFactory.TestSecret, body);
    }
}