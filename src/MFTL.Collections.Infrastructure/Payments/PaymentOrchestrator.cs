using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Infrastructure.Payments;

public sealed class PaymentOrchestrator : IPaymentOrchestrator
{
    public Task<PaymentResult> InitiatePaymentAsync(Guid contributionId, decimal amount, string method, CancellationToken cancellationToken = default)
    {
        var providerReference = $"pay_{contributionId:N}";
        var provider = method.Equals("paystack", StringComparison.OrdinalIgnoreCase) ? "paystack" : "stripe";
        var redirectUrl = $"https://checkout.{provider}.example/pay/{providerReference}";
        return Task.FromResult(new PaymentResult(true, redirectUrl, providerReference));
    }
}
