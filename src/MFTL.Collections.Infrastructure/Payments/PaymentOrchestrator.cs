using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Infrastructure.Payments;

public sealed class PaymentOrchestrator : IPaymentOrchestrator
{
    public Task<PaymentResult> InitiatePaymentAsync(Guid contributionId, decimal amount, string method, CancellationToken cancellationToken = default)
    {
        var providerRef = $"MOCK-{Guid.NewGuid():N}".ToUpperInvariant()[..18];
        var checkoutUrl = $"/payment/mock-checkout?providerReference={providerRef}&contributionId={contributionId}&amount={amount}&method={method}";
        
        // Simulate provider-specific metadata
        var metadata = new Dictionary<string, string>
        {
            ["checkoutUrl"] = checkoutUrl,
            ["method"] = method,
            ["expiresAt"] = DateTimeOffset.UtcNow.AddMinutes(30).ToString("O")
        };

        return Task.FromResult(new PaymentResult(true, checkoutUrl, providerRef, Metadata: metadata));
    }
}
