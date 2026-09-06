namespace OnlineSupermarket.Infrastructure.Payments;

public enum PaymentCallbackOutcome
{
    Processed,
    AlreadyProcessed,
    PaymentNotFound,
    Conflict
}

public interface IPaymentCallbackProcessor
{
    Task<PaymentCallbackOutcome> ProcessAsync(
        string provider,
        PaymentCallbackVerificationResult callback,
        CancellationToken cancellationToken);
}
