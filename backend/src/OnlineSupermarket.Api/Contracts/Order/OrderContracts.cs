using System.Text.Json.Serialization;

namespace OnlineSupermarket.Api.Contracts.Order;

public sealed record OrderListDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc,
    [property: JsonPropertyName("totalAmount")] decimal TotalAmount,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("fulfillmentType")] string FulfillmentType,
    [property: JsonPropertyName("itemCount")] int ItemCount);

public sealed record OrderDetailDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("branchId")] Guid BranchId,
    [property: JsonPropertyName("fulfillmentType")] string FulfillmentType,
    [property: JsonPropertyName("recipientName")] string RecipientName,
    [property: JsonPropertyName("recipientPhone")] string RecipientPhone,
    [property: JsonPropertyName("deliveryAddressSnapshot")] string DeliveryAddressSnapshot,
    [property: JsonPropertyName("subtotal")] decimal Subtotal,
    [property: JsonPropertyName("discountAmount")] decimal DiscountAmount,
    [property: JsonPropertyName("shippingFee")] decimal ShippingFee,
    [property: JsonPropertyName("totalAmount")] decimal TotalAmount,
    [property: JsonPropertyName("promotionCodeSnapshot")] string? PromotionCodeSnapshot,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc,
    [property: JsonPropertyName("updatedAtUtc")] DateTime UpdatedAtUtc,
    [property: JsonPropertyName("items")] IReadOnlyList<OrderItemDto> Items,
    [property: JsonPropertyName("statusHistory")] IReadOnlyList<StatusHistoryDto> StatusHistory,
    [property: JsonPropertyName("payment")] PaymentDto? Payment);

public sealed record OrderItemDto(
    [property: JsonPropertyName("orderItemId")] Guid OrderItemId,
    [property: JsonPropertyName("productId")] Guid ProductId,
    [property: JsonPropertyName("productName")] string ProductName,
    [property: JsonPropertyName("sku")] string Sku,
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("lineTotal")] decimal LineTotal,
    [property: JsonPropertyName("canReview")] bool CanReview,
    [property: JsonPropertyName("reviewId")] Guid? ReviewId);

public sealed record StatusHistoryDto(
    [property: JsonPropertyName("fromStatus")] string FromStatus,
    [property: JsonPropertyName("toStatus")] string ToStatus,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc);

public sealed record PaymentDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("providerTransactionId")] string? ProviderTransactionId,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc,
    [property: JsonPropertyName("isMock")] bool IsMock = false);

public sealed record UpdateOrderStatusRequest(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("note")] string? Note = null);

public sealed record PaginatedOrdersDto(
    [property: JsonPropertyName("data")] IReadOnlyList<OrderListDto> Data,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize);
