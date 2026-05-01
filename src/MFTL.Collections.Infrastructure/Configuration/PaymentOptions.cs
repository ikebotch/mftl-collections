namespace MFTL.Collections.Infrastructure.Configuration;

public class PaymentOptions
{
    public const string SectionName = "Payments";
    public string BaseUrl { get; set; } = string.Empty;
    public string ClientApp { get; set; } = "mftl-collections";
    public string? SharedSecret { get; set; }
}
