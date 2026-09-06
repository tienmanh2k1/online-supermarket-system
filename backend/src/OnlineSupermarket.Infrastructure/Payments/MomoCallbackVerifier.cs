using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace OnlineSupermarket.Infrastructure.Payments;

public sealed class MomoCallbackVerifier(IOptions<MoMoWebhookOptions> options) : IPaymentCallbackVerifier
{
    public string Provider => "MoMo";

    public PaymentCallbackVerificationResult Verify(IReadOnlyDictionary<string, string> data)
    {
        var signature = data.GetValueOrDefault("signature") ?? string.Empty;
        var eventId = data.GetValueOrDefault("transId") ?? string.Empty;
        var orderText = data.GetValueOrDefault("orderId") ?? string.Empty;
        var amountText = data.GetValueOrDefault("amount") ?? string.Empty;
        var code = data.GetValueOrDefault("resultCode") ?? string.Empty;

        var secret = options.Value.Secret;
        var accessKey = options.Value.AccessKey;
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(accessKey))
            return InvalidResult("WEBHOOK_NOT_CONFIGURED", eventId, orderText, code);

        if (string.IsNullOrWhiteSpace(code))
            return InvalidResult("MALFORMED_CALLBACK", eventId, orderText, code);

        if (!CallbackVerifierSupport.TryParseCommon(signature, eventId, orderText, amountText, false, out var orderId, out var amount))
            return InvalidResult("MALFORMED_CALLBACK", eventId, orderText, code);

        var success = code == "0";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var valid = CallbackVerifierSupport.FixedTimeHexEquals(
            signature, hmac.ComputeHash(Encoding.UTF8.GetBytes(CallbackVerifierSupport.MoMoCanonical(data, accessKey))));

        return new PaymentCallbackVerificationResult(
            valid, eventId, orderId, amount, success,
            CallbackVerifierSupport.SanitizedPayload(Provider, eventId, orderId, amount, code),
            valid ? null : "INVALID_SIGNATURE");
    }

    private static PaymentCallbackVerificationResult InvalidResult(string errorCode, string eventId, string orderText, string code)
    {
        Guid.TryParse(orderText, out var orderId);
        return new PaymentCallbackVerificationResult(
            false, eventId, orderId, 0m, code == "0",
            CallbackVerifierSupport.SanitizedPayload("MoMo", eventId, orderId, 0m, code),
            errorCode);
    }
}