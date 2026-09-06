using Microsoft.Extensions.Options;
using OnlineSupermarket.Infrastructure.Payments;

namespace OnlineSupermarket.Infrastructure.Tests.Payments;

public sealed class PaymentCallbackVerifierTests
{
    private const string VnPaySecret = "vnpay-test-secret";
    private const string MoMoSecret = "momo-test-secret";
    private const string MoMoAccessKey = "momo-access-key";
    private static readonly Guid OrderGuid = Guid.Parse("9b0e6ef8-6d4c-4f85-9a3d-1e2f3c4b5a6d");

    // Known-answer vectors computed independently with Python stdlib (RFC 3986 joining
    // for VNPay with %20 for spaces, raw sorted joining for MoMo IPN) — never produced by
    // calling the production verifier. Sources checked 2026-09-06:
    // - VNPay merchant integration guide v2.1.0 (HMAC-SHA512 over ordinal-sorted non-empty
    //   vnp_* fields excluding vnp_SecureHash/vnp_SecureHashType by key; canonical = urlencode(key)=urlencode(value)
    //   joined with &; amount is the smallest unit x100) — developer documentation published at
    //   https://sandbox.vnpayment.vn/psp/help/api or mirror reference implementations (viblo.asia VNPay guide).
    // - MoMo All-in-One IPN callback docs (API 2024-08, https://developers.momo.vn "Confirm Payment IPN"):
    //   HMAC-SHA256 over the 13 IPN fields sorted alphabetically a-z, accessKey injected from config
    //   (not echoed in the POST body), empty optional fields retained as key=, resultCode 0 = success.
    private const string VnPayValidSignature =
        "ab09c3dc5bf0d8762ce2b9e8723a7132b147807850727d29df1c876bdb3e91fa" +
        "1e75709c28c648558869bc519f8da0e3424814a0a54dc003035e4e940036a0ae";
    private const string MoMoValidSignature =
        "d0152b5f15012638795f13e79d99008f3f951160c2a6e2e49e02a4c402d82d8b";

    private static Dictionary<string, string> VnPayData() => new()
    {
        ["vnp_Amount"] = "1000000",
        ["vnp_BankCode"] = "NCB",
        ["vnp_OrderInfo"] = "Thanh toan don hang",
        ["vnp_ResponseCode"] = "00",
        ["vnp_TransactionNo"] = "VNPAY123456",
        ["vnp_TxnRef"] = OrderGuid.ToString(),
        ["vnp_SecureHashType"] = "SHA512",
        ["vnp_SecureHash"] = VnPayValidSignature
    };

    private static Dictionary<string, string> MoMoData() => new()
    {
        ["amount"] = "10000",
        ["extraData"] = "",
        ["message"] = "Successful.",
        ["orderId"] = OrderGuid.ToString(),
        ["orderInfo"] = "Thanh toan don hang",
        ["orderType"] = "momo_wallet",
        ["partnerCode"] = "MOMO",
        ["payType"] = "wallet",
        ["requestId"] = "REQ-1",
        ["responseTime"] = "1725000000000",
        ["resultCode"] = "0",
        ["transId"] = "MOMO987654",
        ["signature"] = MoMoValidSignature
    };

    private static VnPayCallbackVerifier VnPay(string? secret = VnPaySecret) =>
        new(Options.Create(new VnPayWebhookOptions { Secret = secret ?? string.Empty }));

    private static MomoCallbackVerifier MoMo(
        string? secret = MoMoSecret,
        string? accessKey = MoMoAccessKey) =>
        new(Options.Create(new MoMoWebhookOptions { Secret = secret ?? string.Empty, AccessKey = accessKey ?? string.Empty }));

    [Fact]
    public void VnPay_accepts_valid_signature_and_normalizes_amount_unit()
    {
        var result = VnPay().Verify(VnPayData());

        Assert.True(result.IsValidSignature);
        Assert.True(result.IsSuccess);
        Assert.Equal("VNPAY123456", result.ExternalEventId);
        Assert.Equal(OrderGuid, result.OrderId);
        Assert.Equal(10000.00m, result.Amount);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void VnPay_rejects_single_byte_tamper()
    {
        var data = VnPayData();
        data["vnp_Amount"] = "1000001";

        var result = VnPay().Verify(data);

        Assert.False(result.IsValidSignature);
        Assert.Equal("INVALID_SIGNATURE", result.ErrorCode);
    }

    [Fact]
    public void VnPay_rejects_malformed_hex_signature()
    {
        var data = VnPayData();
        data["vnp_SecureHash"] = "zz-zz";

        Assert.Equal("INVALID_SIGNATURE", VnPay().Verify(data).ErrorCode);
    }

    [Fact]
    public void VnPay_rejects_signature_of_wrong_length()
    {
        var data = VnPayData();
        data["vnp_SecureHash"] = "abcd";

        Assert.Equal("INVALID_SIGNATURE", VnPay().Verify(data).ErrorCode);
    }

    [Theory]
    [InlineData("vnp_SecureHash")]
    [InlineData("vnp_TransactionNo")]
    [InlineData("vnp_TxnRef")]
    [InlineData("vnp_Amount")]
    [InlineData("vnp_ResponseCode")]
    public void VnPay_rejects_missing_required_field(string removedField)
    {
        var data = VnPayData();
        data.Remove(removedField);

        var result = VnPay().Verify(data);

        Assert.False(result.IsValidSignature);
        Assert.Equal("MALFORMED_CALLBACK", result.ErrorCode);
    }

    [Theory]
    [InlineData("1.234")]
    [InlineData("1,5")]
    [InlineData("-100")]
    [InlineData("99999999999999999999999999999999999999999999999999")]
    public void VnPay_rejects_culturally_encoded_or_invalid_amount(string rawAmount)
    {
        var data = VnPayData();
        data["vnp_Amount"] = rawAmount;

        Assert.Equal("MALFORMED_CALLBACK", VnPay().Verify(data).ErrorCode);
    }

    [Fact]
    public void VnPay_rejects_empty_order_id()
    {
        var data = VnPayData();
        data["vnp_TxnRef"] = "00000000-0000-0000-0000-000000000000";

        Assert.Equal("MALFORMED_CALLBACK", VnPay().Verify(data).ErrorCode);
    }

    [Fact]
    public void VnPay_rejects_invalid_order_id_format()
    {
        var data = VnPayData();
        data["vnp_TxnRef"] = "not-a-guid";

        Assert.Equal("MALFORMED_CALLBACK", VnPay().Verify(data).ErrorCode);
    }

    [Fact]
    public void VnPay_treats_non_success_code_as_failed_not_success()
    {
        var data = VnPayData();
        data["vnp_ResponseCode"] = "99";
        data["vnp_SecureHash"] =
            "da262fca0dd5be1c10d00ab36be583a84d42deaf3b0f997c7ea8eb628ecee5d6" +
            "bd8ccda9031878c7672242e599f5726236569e0d10e295b1dcfa78f699c2e5e4";

        var result = VnPay().Verify(data);

        Assert.True(result.IsValidSignature);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void VnPay_fails_closed_on_empty_secret()
    {
        var result = VnPay("").Verify(VnPayData());

        Assert.False(result.IsValidSignature);
        Assert.Equal("WEBHOOK_NOT_CONFIGURED", result.ErrorCode);
    }

    [Fact]
    public void VnPay_ignores_non_vnp_fields_for_signing()
    {
        var data = VnPayData();
        data["foo"] = "bar";
        data["signature"] = "should-not-be-used";

        var result = VnPay().Verify(data);

        Assert.True(result.IsValidSignature);
    }

    [Fact]
    public void VnPay_excludes_secure_hash_fields_from_signing()
    {
        var data = VnPayData();
        data["vnp_SecureHashType"] = "DIFFERENT";

        Assert.True(VnPay().Verify(data).IsValidSignature);
    }

    [Fact]
    public void VnPay_sanitized_payload_never_exposes_secret()
    {
        var data = VnPayData();
        data["vnp_Note"] = VnPaySecret;

        var result = VnPay().Verify(data);

        Assert.DoesNotContain(VnPaySecret, result.SanitizedPayload);
        Assert.Contains("VNPAY123456", result.SanitizedPayload);
        Assert.Contains(OrderGuid.ToString(), result.SanitizedPayload);
        Assert.Contains("10000.00", result.SanitizedPayload);
        Assert.Contains("00", result.SanitizedPayload);
        Assert.Contains("\"VNPay\"", result.SanitizedPayload);
    }

    [Fact]
    public void MoMo_accepts_valid_ipn_signature_with_access_key_from_config()
    {
        var result = MoMo().Verify(MoMoData());

        Assert.True(result.IsValidSignature);
        Assert.True(result.IsSuccess);
        Assert.Equal("MOMO987654", result.ExternalEventId);
        Assert.Equal(OrderGuid, result.OrderId);
        Assert.Equal(10000m, result.Amount);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void MoMo_rejects_single_byte_tamper()
    {
        var data = MoMoData();
        data["amount"] = "10001";

        var result = MoMo().Verify(data);

        Assert.False(result.IsValidSignature);
        Assert.Equal("INVALID_SIGNATURE", result.ErrorCode);
    }

    [Fact]
    public void MoMo_rejects_malformed_hex_signature()
    {
        var data = MoMoData();
        data["signature"] = "!!invalid!!";

        Assert.Equal("INVALID_SIGNATURE", MoMo().Verify(data).ErrorCode);
    }

    [Fact]
    public void MoMo_rejects_signature_of_wrong_length()
    {
        var data = MoMoData();
        data["signature"] = "abc";

        Assert.Equal("INVALID_SIGNATURE", MoMo().Verify(data).ErrorCode);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("transId")]
    [InlineData("orderId")]
    [InlineData("amount")]
    [InlineData("resultCode")]
    public void MoMo_rejects_missing_required_field(string removedField)
    {
        var data = MoMoData();
        data.Remove(removedField);

        Assert.Equal("MALFORMED_CALLBACK", MoMo().Verify(data).ErrorCode);
    }

    [Theory]
    [InlineData("1,5")]
    [InlineData("-100")]
    [InlineData("1.23.45")]
    [InlineData("99999999999999999999999999999999999999999999999999")]
    public void MoMo_rejects_culturally_encoded_or_invalid_amount(string rawAmount)
    {
        var data = MoMoData();
        data["amount"] = rawAmount;

        Assert.Equal("MALFORMED_CALLBACK", MoMo().Verify(data).ErrorCode);
    }

    [Fact]
    public void MoMo_rejects_empty_or_invalid_order_id()
    {
        var data = MoMoData();
        data["orderId"] = Guid.Empty.ToString();

        Assert.Equal("MALFORMED_CALLBACK", MoMo().Verify(data).ErrorCode);
    }

    [Fact]
    public void MoMo_treats_non_zero_result_code_as_failed_not_success()
    {
        var data = MoMoData();
        data["resultCode"] = "9000";
        data["signature"] = "fa386638b42a08948925c28fec64d2e22adb3a488cb67fe90a00d55b270c54f2";

        var result = MoMo().Verify(data);

        Assert.True(result.IsValidSignature);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void MoMo_fails_closed_on_empty_secret()
    {
        var result = MoMo("", MoMoAccessKey).Verify(MoMoData());

        Assert.False(result.IsValidSignature);
        Assert.Equal("WEBHOOK_NOT_CONFIGURED", result.ErrorCode);
    }

    [Fact]
    public void MoMo_fails_closed_on_missing_access_key()
    {
        var result = MoMo(MoMoSecret, "").Verify(MoMoData());

        Assert.False(result.IsValidSignature);
        Assert.Equal("WEBHOOK_NOT_CONFIGURED", result.ErrorCode);
    }

    [Fact]
    public void MoMo_ignores_unknown_fields_for_signing()
    {
        var data = MoMoData();
        data["unknownField"] = "momo-must-ignore";

        Assert.True(MoMo().Verify(data).IsValidSignature);
    }

    [Fact]
    public void MoMo_sanitized_payload_never_exposes_secret_or_access_key()
    {
        var data = MoMoData();
        data["extraData"] = MoMoSecret;

        var result = MoMo().Verify(data);

        Assert.DoesNotContain(MoMoSecret, result.SanitizedPayload);
        Assert.DoesNotContain(MoMoAccessKey, result.SanitizedPayload);
        Assert.Contains("MOMO987654", result.SanitizedPayload);
        Assert.Contains(OrderGuid.ToString(), result.SanitizedPayload);
        Assert.Contains("10000", result.SanitizedPayload);
        Assert.Contains("0", result.SanitizedPayload);
        Assert.Contains("\"MoMo\"", result.SanitizedPayload);
    }
}