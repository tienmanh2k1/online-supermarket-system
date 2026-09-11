namespace OnlineSupermarket.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    public string Mode { get; set; } = "Sandbox";
}
