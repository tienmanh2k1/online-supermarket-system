using OnlineSupermarket.Domain.Common;

namespace OnlineSupermarket.Domain.Payments;

public sealed class Payment : Entity
{
    private Payment() { }

    public Guid OrderId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public PaymentStatus Status { get; private set; }
    public decimal Amount { get; private set; }
    public bool IsMock { get; private set; }
    public string? ProviderTransactionId { get; private set; }
    public string? ProviderResponse { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public static Payment Create(Guid orderId, PaymentMethod method, decimal amount, bool isMock = false)
    {
        return new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Method = method,
            Amount = amount,
            IsMock = isMock,
            Status = method == PaymentMethod.COD ? PaymentStatus.PendingCollection : PaymentStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkCompleted(string providerTransactionId, string? response = null)
    {
        if (Status is PaymentStatus.Completed or PaymentStatus.Failed or PaymentStatus.Refunded)
            throw new InvalidOperationException($"Payment cannot transition from {Status} to Completed.");
        if (string.IsNullOrWhiteSpace(providerTransactionId))
            throw new ArgumentException("Provider transaction id is required.", nameof(providerTransactionId));

        Status = PaymentStatus.Completed;
        ProviderTransactionId = providerTransactionId;
        ProviderResponse = response;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string? response = null)
    {
        if (Status is PaymentStatus.Completed or PaymentStatus.Failed or PaymentStatus.Refunded)
            throw new InvalidOperationException($"Payment cannot transition from {Status} to Failed.");
        Status = PaymentStatus.Failed;
        ProviderResponse = response;
    }
}
