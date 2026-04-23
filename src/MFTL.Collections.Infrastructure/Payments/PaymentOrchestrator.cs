using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Infrastructure.Payments;

public sealed class PaymentOrchestrator : IPaymentOrchestrator
{
    public Task<PaymentResult> InitiatePaymentAsync(Guid contributionId, decimal amount, string method, CancellationToken cancellationToken = default)
    {
        // Mock implementation
        return Task.FromResult(new PaymentResult(true, "https://checkout.provider.com/pay/123", Guid.NewGuid().ToString()));
    }
}
