using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OnlineSupermarket.Infrastructure.Payments;

namespace OnlineSupermarket.Infrastructure.Tests.Payments;

public sealed class PaymentCallbackVerifierTests
{
    [Fact]
    public void VnPay_rejects_missing_signature_and_malformed_amount()
    {
        var verifier = new VnPayCallbackVerifier(Options.Create(new PaymentWebhookOptions { VnPaySecret = "secret" }));
        var result = verifier.Verify(new Dictionary<string, string>
        {
            ["transactionId"] = "evt-1", ["orderId"] = Guid.NewGuid().ToString(), ["amount"] = "bad", ["responseCode"] = "00"
        });

        Assert.False(result.IsValidSignature);
        Assert.Equal("MALFORMED_CALLBACK", result.ErrorCode);
    }

    [Fact]
    public void Momo_accepts_a_valid_hmac_signature()
    {
        var data = new Dictionary<string, string>
        {
            ["transactionId"] = "evt-1", ["orderId"] = Guid.NewGuid().ToString(), ["amount"] = "12.50", ["responseCode"] = "0"
        };
        var canonical = string.Join("&", data.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("secret"));
        data["signature"] = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));

        var result = new MomoCallbackVerifier(Options.Create(new PaymentWebhookOptions { MoMoSecret = "secret" })).Verify(data);

        Assert.True(result.IsValidSignature);
        Assert.True(result.IsSuccess);
        Assert.Equal(12.50m, result.Amount);
    }
}
