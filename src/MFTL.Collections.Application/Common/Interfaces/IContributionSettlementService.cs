using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Common.Interfaces;

public interface IContributionSettlementService
{
    Task SettleContributionAsync(Guid contributionId, Guid? paymentId, CancellationToken cancellationToken = default);
}
