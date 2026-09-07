namespace OnlineSupermarket.Infrastructure.Payments;

public sealed class VnPayWebhookOptions
{
    public const string SectionName = "Payments:Webhooks:VNPay";
    public string Secret { get; set; } = string.Empty;
}

public sealed class MoMoWebhookOptions
{
    public const string SectionName = "Payments:Webhooks:MoMo";
    public string Secret { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
}