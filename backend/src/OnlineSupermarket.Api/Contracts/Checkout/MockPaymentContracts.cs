using System.Text.Json.Serialization;

namespace OnlineSupermarket.Api.Contracts.Checkout;

public sealed record CompleteMockPaymentRequest([property: JsonPropertyName("outcome")] string Outcome);

public sealed record MockPaymentDto(
    [property: JsonPropertyName("paymentId")] Guid PaymentId,
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("paymentStatus")] string PaymentStatus,
    [property: JsonPropertyName("orderStatus")] string OrderStatus,
    [property: JsonPropertyName("isMock")] bool IsMock);
