using System.Text.Json.Serialization;

namespace OnlineSupermarket.Api.Contracts.Inventory;

public sealed record InventoryTransactionDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("branchInventoryId")] Guid BranchInventoryId,
    [property: JsonPropertyName("transactionType")] string TransactionType,
    [property: JsonPropertyName("quantityOnHandDelta")] int QuantityOnHandDelta,
    [property: JsonPropertyName("reservedQuantityDelta")] int ReservedQuantityDelta,
    [property: JsonPropertyName("quantityOnHandAfter")] int QuantityOnHandAfter,
    [property: JsonPropertyName("reservedQuantityAfter")] int ReservedQuantityAfter,
    [property: JsonPropertyName("referenceType")] string ReferenceType,
    [property: JsonPropertyName("referenceId")] Guid? ReferenceId,
    [property: JsonPropertyName("actorUserId")] Guid? ActorUserId,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc);

public sealed record PaginatedInventoryTransactionsDto(
    [property: JsonPropertyName("data")] IReadOnlyList<InventoryTransactionDto> Data,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize);

public sealed record ForecastDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("branchInventoryId")] Guid BranchInventoryId,
    [property: JsonPropertyName("productId")] Guid ProductId,
    [property: JsonPropertyName("productName")] string ProductName,
    [property: JsonPropertyName("horizonDays")] int HorizonDays,
    [property: JsonPropertyName("predictedQuantity")] decimal PredictedQuantity,
    [property: JsonPropertyName("actualDataDays")] int ActualDataDays,
    [property: JsonPropertyName("dataQuality")] string DataQuality,
    [property: JsonPropertyName("forecastStartDate")] DateOnly ForecastStartDate,
    [property: JsonPropertyName("forecastEndDate")] DateOnly ForecastEndDate,
    [property: JsonPropertyName("generatedAtUtc")] DateTime GeneratedAtUtc,
    [property: JsonPropertyName("jobRunId")] Guid JobRunId);

public sealed record TriggerForecastRunRequest(
    [property: JsonPropertyName("branchId")] Guid BranchId);

public sealed record TriggerForecastRunResponse(
    [property: JsonPropertyName("jobRunId")] Guid JobRunId,
    [property: JsonPropertyName("statusUrl")] string StatusUrl);