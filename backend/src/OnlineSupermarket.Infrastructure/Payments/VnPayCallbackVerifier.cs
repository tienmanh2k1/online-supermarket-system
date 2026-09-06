using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace OnlineSupermarket.Infrastructure.Payments;

public sealed class VnPayCallbackVerifier(IOptions<PaymentWebhookOptions> options) : IPaymentCallbackVerifier
{
    public string Provider => "VNPay";

    public PaymentCallbackVerificationResult Verify(IReadOnlyDictionary<string, string> data)
    {
        if (!CallbackVerifierSupport.TryCommon(data, out var id, out var orderId, out var amount, out var success, out var error))
            return new(false, id, orderId, amount, success, CallbackVerifierSupport.Payload(data), error);
        var signature = data.GetValueOrDefault("vnp_SecureHash") ?? data.GetValueOrDefault("signature") ?? string.Empty;
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(options.Value.VnPaySecret));
        var valid = !string.IsNullOrWhiteSpace(signature) && CallbackVerifierSupport.FixedTimeHexEquals(
            signature, hmac.ComputeHash(Encoding.UTF8.GetBytes(CallbackVerifierSupport.Canonical(data, signature.Contains("vnp_", StringComparison.OrdinalIgnoreCase) ? "vnp_SecureHash" : "signature"))));
        return new(valid, id, orderId, amount, success, CallbackVerifierSupport.Payload(data), valid ? null : "INVALID_SIGNATURE");
    }
}
