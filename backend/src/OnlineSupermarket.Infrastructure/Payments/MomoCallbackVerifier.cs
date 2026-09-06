using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace OnlineSupermarket.Infrastructure.Payments;

public sealed class MomoCallbackVerifier(IOptions<PaymentWebhookOptions> options) : IPaymentCallbackVerifier
{
    public string Provider => "MoMo";

    public PaymentCallbackVerificationResult Verify(IReadOnlyDictionary<string, string> data)
    {
        if (!CallbackVerifierSupport.TryCommon(data, out var id, out var orderId, out var amount, out var success, out var error))
            return new(false, id, orderId, amount, success, CallbackVerifierSupport.Payload(data), error);
        var signature = data.GetValueOrDefault("signature") ?? data.GetValueOrDefault("signatureValue") ?? string.Empty;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.Value.MoMoSecret));
        var valid = !string.IsNullOrWhiteSpace(signature) && CallbackVerifierSupport.FixedTimeHexEquals(
            signature, hmac.ComputeHash(Encoding.UTF8.GetBytes(CallbackVerifierSupport.Canonical(data, "signature"))));
        return new(valid, id, orderId, amount, success, CallbackVerifierSupport.Payload(data), valid ? null : "INVALID_SIGNATURE");
    }
}
