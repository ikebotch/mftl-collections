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

        var result = await SettleContributionAsync(contribution, paymentId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("Contribution {ContributionId} settled successfully.", contributionId);
        return result;
    }

    public async Task<ContributionSettlementResult> SettleContributionAsync(Contribution contribution, Guid? paymentId, CancellationToken cancellationToken = default)
    {
        if (contribution.RecipientFund == null)
        {
            contribution.RecipientFund = await dbContext.RecipientFunds
                .FirstOrDefaultAsync(f => f.Id == contribution.RecipientFundId, cancellationToken)
                ?? throw new KeyNotFoundException($"Recipient fund {contribution.RecipientFundId} not found.");
        }

        Payment? payment = null;
        if (paymentId.HasValue)
        {
            payment = contribution.PaymentId == paymentId
                ? contribution.Payment
                : await dbContext.Payments.FirstOrDefaultAsync(p => p.Id == paymentId.Value, cancellationToken);

            if (payment == null || payment.Status != PaymentStatus.Succeeded)
            {
                throw new InvalidOperationException($"Successful payment {paymentId} not found.");
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

        var receipt = contribution.Receipt ?? await dbContext.Receipts
            .FirstOrDefaultAsync(r => r.ContributionId == contribution.Id, cancellationToken);

        if (receipt == null)
        {
            receipt = new Receipt
            {
                TenantId = contribution.TenantId == Guid.Empty ? contribution.RecipientFund.TenantId : contribution.TenantId,
                EventId = contribution.EventId,
                RecipientFundId = contribution.RecipientFundId,
                ContributionId = contribution.Id,
                PaymentId = payment?.Id,
                RecordedByUserId = await ResolveRecordedByUserIdAsync(cancellationToken),
                ReceiptNumber = await GenerateUniqueReceiptNumberAsync(cancellationToken),
                IssuedAt = DateTimeOffset.UtcNow,
                Status = ReceiptStatus.Issued,
                Note = contribution.Note,
            };

            dbContext.Receipts.Add(receipt);
            contribution.Receipt = receipt;
        }
        else if (payment?.Id != null && receipt.PaymentId == null)
        {
            receipt.PaymentId = payment.Id;
        }

        return new ContributionSettlementResult(contribution.Id, receipt.Id);
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
