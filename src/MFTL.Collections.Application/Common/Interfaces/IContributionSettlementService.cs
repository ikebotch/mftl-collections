using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Common.Interfaces;

public record ContributionSettlementResult(Guid ContributionId, Guid ReceiptId);

public interface IContributionSettlementService
{
    Task<ContributionSettlementResult> SettleContributionAsync(Guid contributionId, Guid? paymentId, CancellationToken cancellationToken = default);
    Task<ContributionSettlementResult> SettleContributionAsync(Contribution contribution, Guid? paymentId, CancellationToken cancellationToken = default);
}

public interface IReceiptNumberGenerator
{
    string Generate();
}
