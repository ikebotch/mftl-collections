using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Infrastructure.Services;

public sealed class ContributionSettlementService(
    CollectionsDbContext dbContext, 
    ILogger<ContributionSettlementService> logger) : IContributionSettlementService
{
    public async Task SettleContributionAsync(Guid contributionId, Guid? paymentId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Settling contribution {ContributionId} with payment {PaymentId}", contributionId, paymentId);

        var contribution = await dbContext.Contributions
            .Include(c => c.RecipientFund)
            .FirstOrDefaultAsync(c => c.Id == contributionId, cancellationToken);

        if (contribution == null)
        {
            throw new InvalidOperationException($"Contribution {contributionId} not found.");
        }

        if (paymentId.HasValue)
        {
            var payment = await dbContext.Payments.FindAsync(new object[] { paymentId.Value }, cancellationToken);
            if (payment == null || payment.Status != PaymentStatus.Succeeded)
            {
                throw new InvalidOperationException($"Successful payment {paymentId} not found.");
            }
            contribution.PaymentId = paymentId;
        }

        // Single controlled mutation path
        contribution.Status = ContributionStatus.Completed;
        
        // Update denormalized totals in a controlled way
        contribution.RecipientFund.CollectedAmount += contribution.Amount;

        await dbContext.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("Contribution {ContributionId} settled successfully.", contributionId);
    }
}
