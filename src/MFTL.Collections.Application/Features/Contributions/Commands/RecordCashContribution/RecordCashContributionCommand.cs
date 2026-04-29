using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Features.Contributions.Commands.RecordCashContribution;

[HasPermission("contributions.record_cash")]
public record RecordCashContributionCommand(
    Guid EventId,
    Guid RecipientFundId,
    decimal Amount,
    string Currency,
    string? ContributorName,
    string ContributorPhone,
    string? ContributorEmail,
    bool Anonymous,
    string PaymentMethod,
    string? Note,
    string? ExplicitUserId,
    string? Pin,
    string? IdempotencyKey,
    DateTimeOffset? CollectedAt = null) : IRequest<CashContributionResult>, IHasScope
{
    public Guid? GetScopeId() => EventId;
}

public class RecordCashContributionCommandValidator : AbstractValidator<RecordCashContributionCommand>
{
    public RecordCashContributionCommandValidator()
    {
        RuleFor(v => v.EventId).NotEmpty();
        RuleFor(v => v.RecipientFundId).NotEmpty();
        RuleFor(v => v.Amount).GreaterThan(0);
        RuleFor(v => v.Currency).NotEmpty();
        RuleFor(v => v.ContributorPhone).NotEmpty().MinimumLength(7);
        RuleFor(v => v.PaymentMethod).NotEmpty();
        RuleFor(v => v.Pin).NotEmpty().Length(4);
        When(v => !v.Anonymous, () =>
        {
            RuleFor(v => v.ContributorName).NotEmpty().MinimumLength(2);
        });
    }
}

public class RecordCashContributionCommandHandler(
    IApplicationDbContext dbContext,
    IAccessPolicyResolver policyResolver,
    IContributionSettlementService settlementService) : IRequestHandler<RecordCashContributionCommand, CashContributionResult>
{
    public async Task<CashContributionResult> Handle(RecordCashContributionCommand request, CancellationToken cancellationToken)
    {
        var policy = await policyResolver.ResolvePolicyAsync();
        var context = await policyResolver.GetAccessContextAsync();

        if (!policy.CanRecordCollection(request.EventId, request.RecipientFundId))
        {
            throw new UnauthorizedAccessException("You do not have access to record contributions for the specified event or fund.");
        }

        var auth0Id = context.Auth0Id;
        
        if (!string.IsNullOrWhiteSpace(request.ExplicitUserId) && request.ExplicitUserId != auth0Id)
        {
            if (!context.IsPlatformAdmin)
            {
                throw new UnauthorizedAccessException("You are not authorized to record contributions for another collector.");
            }
            auth0Id = request.ExplicitUserId;
        }

        var collector = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Auth0Id == auth0Id, cancellationToken);


        if (collector == null)
        {
            throw new UnauthorizedAccessException("Collector profile not found.");
        }

        if (!collector.IsActive)
        {
            throw new UnauthorizedAccessException("Collector is inactive.");
        }

        // PIN Verification
        if (string.IsNullOrEmpty(collector.Pin))
        {
             // If PIN is not set, we might want to allow it or force set? 
             // The user said "collectors have a pin", implying it should be there.
             // For now, I'll allow it if not set to prevent breaking existing collectors, 
             // but if request has a PIN or it's required, we check it.
             // Actually, let's be strict as requested.
             throw new UnauthorizedAccessException("Collector PIN is not set. Please set a PIN in settings.");
        }

        if (collector.Pin != request.Pin)
        {
            throw new UnauthorizedAccessException("Invalid collector PIN.");
        }


        var @event = await dbContext.Events
            .Include(e => e.SmsTemplate)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);
        if (@event == null) throw new KeyNotFoundException("Event not found.");



        var recipientFund = await dbContext.RecipientFunds
            .FirstOrDefaultAsync(f => f.Id == request.RecipientFundId && f.EventId == request.EventId, cancellationToken);

        if (recipientFund == null)
        {
            throw new KeyNotFoundException("Recipient fund not found.");
        }

        var contributor = new Contributor
        {
            TenantId = @event.TenantId,
            BranchId = @event.BranchId,
            Name = request.ContributorName?.Trim() ?? "Anonymous",
            Email = request.ContributorEmail?.Trim() ?? string.Empty,
            PhoneNumber = request.ContributorPhone.Trim(),
            IsAnonymous = request.Anonymous,
        };

        dbContext.Contributors.Add(contributor);

        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var existingContribution = await dbContext.Contributions
                .Include(c => c.Receipt)
                .FirstOrDefaultAsync(c => c.Reference == request.IdempotencyKey, cancellationToken);

            if (existingContribution != null)
            {
                return new CashContributionResult(
                    existingContribution.Id, 
                    existingContribution.Receipt?.Id, 
                    existingContribution.Status.ToString());
            }
        }

        var contribution = new Contribution
        {
            TenantId = @event.TenantId,
            BranchId = @event.BranchId,
            EventId = request.EventId,
            RecipientFundId = request.RecipientFundId,
            RecipientFund = recipientFund,
            Contributor = contributor,
            Amount = request.Amount,
            Currency = request.Currency,
            ContributorName = request.ContributorName?.Trim() ?? "Anonymous",
            IsAnonymous = request.Anonymous,
            Method = request.PaymentMethod,
            Status = ContributionStatus.RecordedCash,
            Note = request.Note,
            Reference = request.IdempotencyKey
        };

        dbContext.Contributions.Add(contribution);
        var settlement = await settlementService.SettleContributionAsync(contribution, null, collector.Id, request.CollectedAt, cancellationToken);
        
        // Ensure the receipt also gets the BranchId if possible
        if (contribution.Receipt != null)
        {
            contribution.Receipt.BranchId = @event.BranchId;
        }

        // Add Domain Events for asynchronous processing (Outbox Pattern)
        contribution.AddDomainEvent(new Domain.Events.ContributionRecordedEvent(
            contribution.Id,
            contribution.TenantId,
            contribution.BranchId,
            contribution.EventId,
            contribution.RecipientFundId,
            contribution.Amount,
            contribution.Currency,
            contribution.ContributorName,
            contributor.Email,
            contributor.PhoneNumber));

        if (contribution.Receipt != null)
        {
            contribution.Receipt.AddDomainEvent(new Domain.Events.ReceiptIssuedEvent(
                contribution.Receipt.Id,
                contribution.Receipt.TenantId,
                contribution.Receipt.BranchId,
                contribution.Id,
                contribution.Receipt.ReceiptNumber,
                contribution.ContributorName,
                contributor.Email,
                contributor.PhoneNumber,
                contribution.Amount,
                contribution.Currency,
                @event.Title));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CashContributionResult(contribution.Id, settlement.ReceiptId, contribution.Status.ToString());
    }
}
