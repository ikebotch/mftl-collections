using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Infrastructure.Services;

public sealed class ContributionSettlementService(
    CollectionsDbContext dbContext, 
    ICurrentUserService currentUserService,
    IReceiptNumberGenerator receiptNumberGenerator,
    ILogger<ContributionSettlementService> logger) : IContributionSettlementService
{
    public async Task<ContributionSettlementResult> SettleContributionAsync(Guid contributionId, Guid? paymentId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Settling contribution {ContributionId} with payment {PaymentId}", contributionId, paymentId);

        var contribution = await dbContext.Contributions
            .Include(c => c.RecipientFund)
            .Include(c => c.Payment)
            .Include(c => c.Receipt)
            .FirstOrDefaultAsync(c => c.Id == contributionId, cancellationToken);

        if (contribution == null)
        {
            throw new KeyNotFoundException($"Contribution {contributionId} not found.");
        }

        var result = await SettleContributionAsync(contribution, paymentId, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("Contribution {ContributionId} settled successfully.", contributionId);
        return result;
    }

    public async Task<ContributionSettlementResult> SettleContributionAsync(
        Contribution contribution,
        Guid? paymentId,
        Guid? recordedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (contribution.RecipientFund == null)
        {
            contribution.RecipientFund = await dbContext.RecipientFunds
                .IgnoreQueryFilters() // Internal system resolution
                .FirstOrDefaultAsync(f => f.Id == contribution.RecipientFundId, cancellationToken)
                ?? throw new KeyNotFoundException($"Recipient fund {contribution.RecipientFundId} not found.");
        }

        ValidateContributionSettlementBoundary(contribution);

        Payment? payment = null;
        if (paymentId.HasValue)
        {
            payment = contribution.PaymentId == paymentId
                ? contribution.Payment
                : await dbContext.Payments
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == paymentId.Value, cancellationToken);

            if (payment == null || payment.Status != PaymentStatus.Succeeded)
            {
                throw new InvalidOperationException($"Successful payment {paymentId} not found.");
            }

            if (payment.TenantId != contribution.TenantId || payment.ContributionId != contribution.Id)
            {
                throw new InvalidOperationException("Payment does not belong to the contribution tenant.");
            }

            contribution.PaymentId = paymentId;
            contribution.Payment = payment;
        }

        var wasAlreadyCompleted = contribution.Status == ContributionStatus.Completed;
        contribution.Status = ContributionStatus.Completed;

        if (!wasAlreadyCompleted)
        {
            contribution.RecipientFund.CollectedAmount += contribution.Amount;
        }

        // Idempotency: use existing receipt if present
        var receipt = contribution.Receipt ?? await dbContext.Receipts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.ContributionId == contribution.Id, cancellationToken);

        if (receipt == null)
        {
            receipt = new Receipt
            {
                // Ensure IDs are copied from contribution to maintain scoping
                TenantId = contribution.TenantId,
                BranchId = contribution.BranchId,
                EventId = contribution.EventId,
                RecipientFundId = contribution.RecipientFundId,
                ContributionId = contribution.Id,
                PaymentId = payment?.Id,
                RecordedByUserId = recordedByUserId ?? await ResolveRecordedByUserIdAsync(cancellationToken),
                ReceiptNumber = await GenerateUniqueReceiptNumberAsync(cancellationToken),
                IssuedAt = DateTimeOffset.UtcNow,
                Status = ReceiptStatus.Issued,
                Note = contribution.Note
            };

            dbContext.Receipts.Add(receipt);
            contribution.Receipt = receipt;
        }
        else
        {
            // Update linkage if payment was just matched
            if (payment?.Id != null && receipt.PaymentId == null)
            {
                receipt.PaymentId = payment.Id;
            }
            
            // Ensure status sync
            if (receipt.Status != ReceiptStatus.Issued)
            {
                receipt.Status = ReceiptStatus.Issued;
            }
        }

        return new ContributionSettlementResult(contribution.Id, receipt.Id);
    }

    private static void ValidateContributionSettlementBoundary(Contribution contribution)
    {
        if (contribution.RecipientFund.TenantId != contribution.TenantId)
        {
            throw new InvalidOperationException("Recipient fund does not belong to the contribution tenant.");
        }

        if (contribution.RecipientFund.BranchId != contribution.BranchId)
        {
            throw new InvalidOperationException("Recipient fund does not belong to the contribution branch.");
        }

        if (contribution.RecipientFund.EventId != contribution.EventId)
        {
            throw new InvalidOperationException("Recipient fund does not belong to the contribution event.");
        }
    }

    private async Task<Guid?> ResolveRecordedByUserIdAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUserService.UserId))
        {
            return null;
        }

        return await dbContext.Users
            .Where(user => user.Auth0Id == currentUserService.UserId)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string> GenerateUniqueReceiptNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var receiptNumber = receiptNumberGenerator.Generate();
            var exists = await dbContext.Receipts.AnyAsync(r => r.ReceiptNumber == receiptNumber, cancellationToken);
            if (!exists)
            {
                return receiptNumber;
            }
        }

        return $"RCT-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}".ToUpperInvariant()[..23];
    }
}
