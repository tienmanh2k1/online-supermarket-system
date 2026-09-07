using System.Text.Json.Serialization;

namespace OnlineSupermarket.Api.Contracts.Recommendation;

public sealed record RecommendationItemDto(
    [property: JsonPropertyName("productId")] Guid ProductId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("imageUrl")] string? ImageUrl,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("availableQuantity")] int? AvailableQuantity,
    [property: JsonPropertyName("score")] decimal Score,
    [property: JsonPropertyName("reason")] string Reason);

public sealed record RecommendationResponse(
    [property: JsonPropertyName("sourceScope")] string? SourceScope,
    [property: JsonPropertyName("generatedAtUtc")] DateTime? GeneratedAtUtc,
    [property: JsonPropertyName("items")] IReadOnlyList<RecommendationItemDto> Items);

public sealed record RecommendationSampleResponse(
    [property: JsonPropertyName("jobRunId")] Guid JobRunId,
    [property: JsonPropertyName("generatedAtUtc")] DateTime GeneratedAtUtc,
    [property: JsonPropertyName("expiresAtUtc")] DateTime ExpiresAtUtc,
    [property: JsonPropertyName("algorithmVersion")] string AlgorithmVersion,
    [property: JsonPropertyName("items")] IReadOnlyList<RecommendationSampleItemDto> Items);

public sealed record RecommendationSampleItemDto(
    [property: JsonPropertyName("productId")] Guid ProductId,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("audienceKey")] string AudienceKey,
    [property: JsonPropertyName("score")] decimal Score,
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("reason")] string Reason);

public sealed record TriggerRecommendationRunResponse(
    [property: JsonPropertyName("jobRunId")] Guid JobRunId,
    [property: JsonPropertyName("statusUrl")] string StatusUrl);