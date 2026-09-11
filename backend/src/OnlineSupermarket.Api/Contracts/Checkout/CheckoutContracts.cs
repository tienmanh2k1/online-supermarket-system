using System.Text.Json.Serialization;

namespace OnlineSupermarket.Api.Contracts.Checkout;

public sealed record CheckoutRequest(
    [property: JsonPropertyName("fulfillmentType")] string FulfillmentType,
    [property: JsonPropertyName("deliveryAddressId")] Guid? DeliveryAddressId = null,
    [property: JsonPropertyName("recipientName")] string? RecipientName = null,
    [property: JsonPropertyName("recipientPhone")] string? RecipientPhone = null,
    [property: JsonPropertyName("deliveryAddress")] string? DeliveryAddress = null,
    [property: JsonPropertyName("couponCode")] string? CouponCode = null);

public sealed record CouponValidationRequest(
    [property: JsonPropertyName("code")] string Code);

public sealed record CouponValidationResponse(
    [property: JsonPropertyName("valid")] bool Valid,
    [property: JsonPropertyName("discountAmount")] decimal DiscountAmount,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("message")] string Message);

public sealed record CheckoutResponse(
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("subtotal")] decimal Subtotal,
    [property: JsonPropertyName("discountAmount")] decimal DiscountAmount,
    [property: JsonPropertyName("shippingFee")] decimal ShippingFee,
    [property: JsonPropertyName("totalAmount")] decimal TotalAmount,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("payment")] PaymentInitDto? Payment);

public sealed record PaymentInitDto(
    [property: JsonPropertyName("paymentId")] Guid PaymentId,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("checkoutUrl")] string? CheckoutUrl = null,
    [property: JsonPropertyName("isMock")] bool IsMock = false);

public sealed record PaymentOptionsDto(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("onlineEnabled")] bool OnlineEnabled,
    [property: JsonPropertyName("disabledReason")] string? DisabledReason);

public sealed record PaymentRequest(
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("method")] string Method);

public sealed record PaymentCallbackRequest(
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("data")] Dictionary<string, string> Data);
