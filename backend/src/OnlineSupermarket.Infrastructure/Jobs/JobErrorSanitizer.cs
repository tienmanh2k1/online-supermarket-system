using System.Text.RegularExpressions;

namespace OnlineSupermarket.Infrastructure.Jobs;

public static class JobErrorSanitizer
{
    private static readonly string KeyPattern = "password|passwd|secret|token|authorization|api[_-]?key|signature|access[_-]?token";

    private static readonly Regex[] RedactionPatterns =
    [
        // Bearer tokens first so an "Authorization: Bearer xyz" value cannot swallow the token.
        new(@"(?i)Bearer\s+[^\s]+", RegexOptions.Compiled),
        // Quoted or unquoted credential assignments: Password=abc, "secret" : "abc", "token":"xyz", 'api_key':'v'
        // Supports escaped characters within quotes (e.g. "prefix\"AUDIT_MARKER") and optional closing quotes before ':'
        new($"""(?i)(\b(?:{KeyPattern})["']?\s*[:=])\s*(?:"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|[^\s,;]+)""", RegexOptions.Compiled),
        // Sensitive query-string parameter values in URIs
        new($"(?i)([?&](?:{KeyPattern})=)[^&\\s]*", RegexOptions.Compiled),
    ];

    public static string Sanitize(Exception exception, int maxLength = 1000)
    {
        if (exception == null) return string.Empty;

        var message = $"{exception.GetType().Name}: {exception.Message}";
        foreach (var pattern in RedactionPatterns)
        {
            message = pattern.Replace(message, Redact);
        }

        message = message.Replace('\r', ' ').Replace('\n', ' ');
        var stackMarker = message.IndexOf(" at ", StringComparison.Ordinal);
        if (stackMarker >= 0) message = message[..stackMarker];

        if (message.Length > maxLength)
        {
            return message.Substring(0, maxLength - 3) + "...";
        }

        return message;
    }

    private static string Redact(Match match)
        => (match.Groups.Count > 1 && match.Groups[1].Success ? match.Groups[1].Value : string.Empty) + "[REDACTED]";
}