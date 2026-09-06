using OnlineSupermarket.Infrastructure.Jobs;

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
        Assert.True(result.Length <= 1000);
    }
}
