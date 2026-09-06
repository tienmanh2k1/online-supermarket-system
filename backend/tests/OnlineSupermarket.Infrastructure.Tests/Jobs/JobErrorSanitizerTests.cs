using OnlineSupermarket.Infrastructure.Jobs;
using Xunit;

namespace OnlineSupermarket.Infrastructure.Tests.Jobs;

public sealed class JobErrorSanitizerTests
{
    [Fact]
    public void Sanitizes_credentials_and_stack_trace()
    {
        var result = JobErrorSanitizer.Sanitize(new InvalidOperationException("Password=secret Authorization: Bearer abc123\n at Hidden.Stack()"));

        Assert.Contains("InvalidOperationException", result);
        Assert.DoesNotContain("secret", result);
        Assert.DoesNotContain("abc123", result);
        Assert.DoesNotContain("Hidden.Stack", result);
        Assert.DoesNotContain('\n', result);
    }

    [Fact]
    public void Redacts_quoted_credentials()
    {
        var result = JobErrorSanitizer.Sanitize(new InvalidOperationException(
            "connection failed secret=\"s3cr3t-quoted\" and token='tok-value' and api_key: my-key"));

        Assert.DoesNotContain("s3cr3t-quoted", result);
        Assert.DoesNotContain("tok-value", result);
        Assert.DoesNotContain("my-key", result);
        Assert.Contains("secret=[REDACTED]", result);
        Assert.Contains("api_key:", result);
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void Redacts_sensitive_uri_query_values()
    {
        var result = JobErrorSanitizer.Sanitize(new InvalidOperationException(
            "GET https://api.example.com/pay?orderId=1&signature=abc123&password=hunter2&amount=100 failed"));

        Assert.DoesNotContain("abc123", result);
        Assert.DoesNotContain("hunter2", result);
        Assert.Contains("orderId=1", result);
        Assert.Contains("[REDACTED]", result);
        Assert.Contains("failed", result);
    }

    [Fact]
    public void Keeps_plain_text_query_values_intact()
    {
        var result = JobErrorSanitizer.Sanitize(new InvalidOperationException(
            "GET /search?q=query&sort=asc failed"));

        Assert.Contains("q=query", result);
        Assert.Contains("sort=asc", result);
    }

    [Fact]
    public void Truncates_undersized_messages_untouched_and_at_length_boundary()
    {
        var prefix = "InvalidOperationException: ";
        var hundred = new string('a', 100);
        Assert.Equal(prefix + hundred, JobErrorSanitizer.Sanitize(new InvalidOperationException(hundred)));

        var exact = new string('b', 1000 - prefix.Length);
        var atBoundary = JobErrorSanitizer.Sanitize(new InvalidOperationException(exact));
        Assert.Equal(1000, atBoundary.Length);
        Assert.DoesNotContain("...", atBoundary);
    }

    [Fact]
    public void Truncates_overlong_messages_to_limit_with_ellipsis()
    {
        var overlong = new string('c', 5000);

        var truncated = JobErrorSanitizer.Sanitize(new InvalidOperationException(overlong));

        Assert.Equal(1000, truncated.Length);
        Assert.EndsWith("...", truncated);
    }

    [Fact]
    public void Truncation_boundary_uses_stable_prefix()
    {
        var prefix = "InvalidOperationException: ";
        var one = JobErrorSanitizer.Sanitize(new InvalidOperationException(new string('x', 2000)));
        var two = JobErrorSanitizer.Sanitize(new InvalidOperationException(new string('y', 2000)));

        Assert.Equal(1000, one.Length);
        Assert.Equal(1000, two.Length);
        Assert.StartsWith(prefix, one);
        Assert.StartsWith(prefix, two);
        Assert.EndsWith("x...", one);
        Assert.EndsWith("y...", two);
    }

    private static string RedactionMarker() => "[REDACTED]";
}