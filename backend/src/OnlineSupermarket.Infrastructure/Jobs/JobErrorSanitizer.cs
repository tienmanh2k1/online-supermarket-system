namespace OnlineSupermarket.Infrastructure.Jobs;

public static class JobErrorSanitizer
{
    public static string Sanitize(Exception exception, int maxLength = 1000)
    {
        if (exception == null) return string.Empty;

        var message = $"{exception.GetType().Name}: {exception.Message}";
        message = System.Text.RegularExpressions.Regex.Replace(message, @"(?i)Bearer\s+[^\s]+", "Bearer [REDACTED]");
        message = System.Text.RegularExpressions.Regex.Replace(message, @"(?i)(password|secret|token|authorization)\s*[:=]\s*[^\s,;]+", "$1=[REDACTED]");
        message = message.Replace('\r', ' ').Replace('\n', ' ');
        var stackMarker = message.IndexOf(" at ", StringComparison.Ordinal);
        if (stackMarker >= 0) message = message[..stackMarker];
        if (message.Length > maxLength)
        {
            return message.Substring(0, maxLength - 3) + "...";
        }
        
        return message;
    }
}
