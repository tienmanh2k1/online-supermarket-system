using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace OnlineSupermarket.Infrastructure.Payments;

public sealed class VnPayCallbackVerifier(IOptions<VnPayWebhookOptions> options) : IPaymentCallbackVerifier
{
    public string Provider => "VNPay";

    public PaymentCallbackVerificationResult Verify(IReadOnlyDictionary<string, string> data)
    {
        var signature = data.GetValueOrDefault("vnp_SecureHash") ?? string.Empty;
        var eventId = data.GetValueOrDefault("vnp_TransactionNo") ?? string.Empty;
        var orderText = data.GetValueOrDefault("vnp_TxnRef") ?? string.Empty;
        var amountText = data.GetValueOrDefault("vnp_Amount") ?? string.Empty;
        var code = data.GetValueOrDefault("vnp_ResponseCode") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(options.Value.Secret))
            return InvalidResult("WEBHOOK_NOT_CONFIGURED", eventId, orderText, code);

        if (string.IsNullOrWhiteSpace(code))
            return InvalidResult("MALFORMED_CALLBACK", eventId, orderText, code);

        if (!CallbackVerifierSupport.TryParseCommon(signature, eventId, orderText, amountText, true, out var orderId, out var amount))
            return InvalidResult("MALFORMED_CALLBACK", eventId, orderText, code);

        var success = code == "00";
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(options.Value.Secret));
        var valid = CallbackVerifierSupport.FixedTimeHexEquals(
            signature, hmac.ComputeHash(Encoding.UTF8.GetBytes(CallbackVerifierSupport.VnPayCanonical(data))));

        return new PaymentCallbackVerificationResult(
            valid, eventId, orderId, amount, success,
            CallbackVerifierSupport.SanitizedPayload(Provider, eventId, orderId, amount, code),
            valid ? null : "INVALID_SIGNATURE");
    }

    private static PaymentCallbackVerificationResult InvalidResult(string errorCode, string eventId, string orderText, string code)
    {
        Guid.TryParse(orderText, out var orderId);
        return new PaymentCallbackVerificationResult(
            false, eventId, orderId, 0m, code == "00",
            CallbackVerifierSupport.SanitizedPayload("VNPay", eventId, orderId, 0m, code),
            errorCode);
    }
}