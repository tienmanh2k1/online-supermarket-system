using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OnlineSupermarket.Infrastructure.Payments;

internal static class CallbackVerifierSupport
{
    public static string Canonical(IReadOnlyDictionary<string, string> data, string signatureKey)
        => string.Join("&", data.Where(x => !x.Key.Equals(signatureKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={x.Value}"));

    public static bool FixedTimeHexEquals(string expected, byte[] actual)
    {
        var actualHex = Convert.ToHexString(actual);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected.ToLowerInvariant()),
            Encoding.ASCII.GetBytes(actualHex.ToLowerInvariant()));
    }

    public static bool TryCommon(IReadOnlyDictionary<string, string> data, out string eventId, out Guid orderId, out decimal amount, out bool success, out string error)
    {
        eventId = data.GetValueOrDefault("transactionId") ?? data.GetValueOrDefault("vnp_TransactionNo") ?? string.Empty;
        var order = data.GetValueOrDefault("orderId") ?? data.GetValueOrDefault("vnp_TxnRef") ?? string.Empty;
        var amountText = data.GetValueOrDefault("amount") ?? data.GetValueOrDefault("vnp_Amount") ?? string.Empty;
        var code = data.GetValueOrDefault("responseCode") ?? data.GetValueOrDefault("vnp_ResponseCode") ?? string.Empty;
        success = code is "0" or "00";
        orderId = Guid.Empty;
        amount = 0m;
        if (string.IsNullOrWhiteSpace(eventId) || !Guid.TryParse(order, out orderId) ||
            !decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            error = "MALFORMED_CALLBACK";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public static string Payload(IReadOnlyDictionary<string, string> data) => JsonSerializer.Serialize(data);
}
