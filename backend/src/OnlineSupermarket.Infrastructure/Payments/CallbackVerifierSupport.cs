using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OnlineSupermarket.Infrastructure.Payments;

internal static class CallbackVerifierSupport
{
    private static readonly string[] MomoSigningFields =
    {
        "accessKey", "amount", "extraData", "message", "orderId", "orderInfo", "orderType",
        "partnerCode", "payType", "requestId", "responseTime", "resultCode", "transId"
    };

    public static bool TryParseCommon(
        string signature,
        string eventId,
        string orderText,
        string amountText,
        bool scaledBy100,
        out Guid orderId,
        out decimal amount)
    {
        orderId = Guid.Empty;
        amount = 0m;
        return !string.IsNullOrWhiteSpace(signature)
            && !string.IsNullOrWhiteSpace(eventId)
            && Guid.TryParse(orderText, out orderId)
            && orderId != Guid.Empty
            && TryParseAmount(amountText, scaledBy100, out amount);
    }

    public static bool TryParseAmount(string text, bool scaledBy100, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        if (!decimal.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out amount))
            return false;
        if (scaledBy100)
            amount /= 100m;
        return true;
    }

    public static string VnPayCanonical(IReadOnlyDictionary<string, string> data)
    {
        var builder = new StringBuilder();
        var appended = false;
        foreach (var pair in data
                     .Where(x => x.Key.StartsWith("vnp_", StringComparison.Ordinal)
                                 && x.Key is not "vnp_SecureHash" and not "vnp_SecureHashType"
                                 && !string.IsNullOrEmpty(x.Value))
                     .OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (appended) builder.Append('&');
            builder.Append(Rfc3986Encode(pair.Key)).Append('=').Append(Rfc3986Encode(pair.Value));
            appended = true;
        }

        return builder.ToString();
    }

    public static string MoMoCanonical(IReadOnlyDictionary<string, string> data, string accessKey)
    {
        var combined = new Dictionary<string, string>(StringComparer.Ordinal) { ["accessKey"] = accessKey };
        foreach (var field in MomoSigningFields)
        {
            if (field == "accessKey") continue;
            if (data.TryGetValue(field, out var value))
                combined[field] = value;
        }

        return string.Join("&", combined.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}={x.Value}"));
    }

    public static bool FixedTimeHexEquals(string expected, byte[] actual)
    {
        var actualHex = Convert.ToHexString(actual);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected.ToLowerInvariant()),
            Encoding.ASCII.GetBytes(actualHex.ToLowerInvariant()));
    }

    public static string SanitizedPayload(string provider, string eventId, Guid orderId, decimal amount, string statusCode)
        => $"{{\"provider\":\"{provider}\",\"eventId\":\"{eventId}\",\"orderId\":\"{orderId}\",\"amount\":\"{amount.ToString("0.00", CultureInfo.InvariantCulture)}\",\"status\":\"{statusCode}\"}}";

    private static string Rfc3986Encode(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            if ((b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z') || (b >= '0' && b <= '9')
                || b == '-' || b == '_' || b == '.' || b == '~')
            {
                builder.Append((char)b);
            }
            else
            {
                builder.Append('%').Append(b.ToString("X2"));
            }
        }

        return builder.ToString();
    }
}