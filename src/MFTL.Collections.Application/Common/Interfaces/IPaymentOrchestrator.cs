using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Common.Interfaces;

public interface IPaymentOrchestrator
{
    Task<PaymentResult> InitiatePaymentAsync(Guid contributionId, decimal amount, string method, CancellationToken cancellationToken = default);
}
