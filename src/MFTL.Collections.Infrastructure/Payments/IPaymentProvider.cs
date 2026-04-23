using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Infrastructure.Payments;

public interface IPaymentProvider
{
    string ProviderName { get; }
    bool VerifySignature(string payload, string signature, string secret);
    (Guid ContributionId, string ProviderReference, PaymentStatus Status) ParseWebhook(string payload);
}

public class StripePaymentProvider : IPaymentProvider
{
    public string ProviderName => "Stripe";

    public bool VerifySignature(string payload, string signature, string secret)
    {
        // Real implementation would use Stripe.net
        return true; 
    }

    public (Guid ContributionId, string ProviderReference, PaymentStatus Status) ParseWebhook(string payload)
    {
        // Parse JSON and extract IDs
        return (Guid.Empty, "ref_placeholder", PaymentStatus.Succeeded);
    }
}
