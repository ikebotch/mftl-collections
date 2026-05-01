namespace MFTL.Collections.Infrastructure.Configuration;

public class PaymentOptions
{
    public const string SectionName = "Payments";
    public string BaseUrl { get; set; } = string.Empty;
    public string ClientApp { get; set; } = "mftl-collections";
    public string CardProvider { get; set; } = string.Empty;
    public bool AllowStripeCardFallback { get; set; }
    public InternalPaymentOptions Internal { get; set; } = new();

    // Compatibility for existing code if any
    public string? SharedSecret => Internal.SharedSecret;
}

public class InternalPaymentOptions
{
    public string? SharedSecret { get; set; }
}
