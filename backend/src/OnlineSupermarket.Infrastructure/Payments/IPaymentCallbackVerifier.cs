namespace OnlineSupermarket.Infrastructure.Payments;

public interface IPaymentCallbackVerifier
{
    string Provider { get; }
    PaymentCallbackVerificationResult Verify(IReadOnlyDictionary<string, string> data);
}
