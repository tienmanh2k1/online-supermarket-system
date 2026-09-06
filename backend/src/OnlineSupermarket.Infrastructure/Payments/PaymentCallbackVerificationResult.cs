namespace OnlineSupermarket.Infrastructure.Payments;

public sealed record PaymentCallbackVerificationResult(
    bool IsValidSignature,
    string ExternalEventId,
    Guid OrderId,
    decimal Amount,
    bool IsSuccess,
    string SanitizedPayload,
    string? ErrorCode);
