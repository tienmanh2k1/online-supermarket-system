namespace OnlineSupermarket.Infrastructure.Payments;

public sealed class PaymentWebhookOptions
{
    public const string SectionName = "PaymentWebhooks";
    public string VnPaySecret { get; set; } = string.Empty;
    public string MoMoSecret { get; set; } = string.Empty;
}
