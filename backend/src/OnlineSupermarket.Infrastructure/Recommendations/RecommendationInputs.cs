namespace OnlineSupermarket.Infrastructure.Recommendations;

public sealed record ScoringInput(
    IReadOnlyList<ProductCandidate> Products,
    IReadOnlyList<ViewSignal> Views,
    IReadOnlyList<PurchaseSignal> Purchases,
    DateTime NowUtc);

public readonly record struct ProductCandidate(
    Guid ProductId,
    Guid CategoryId,
    Guid BrandId,
    bool IsActive = true);

public readonly record struct ViewSignal(
    Guid ProductId,
    Guid? UserId,
    Guid? AnonymousSessionId,
    DateTime ViewedAtUtc);

public readonly record struct PurchaseSignal(
    Guid ProductId,
    Guid? UserId,
    DateTime CompletedAtUtc,
    int Quantity);

public readonly record struct ScoredProduct(
    Guid ProductId,
    decimal Score,
    string Reason);