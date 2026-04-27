using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Contributions.Commands.RecordCashContribution;

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
    string? ExplicitUserId) : IRequest<CashContributionResult>;

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
        When(v => !v.Anonymous, () =>
        {
            RuleFor(v => v.ContributorName).NotEmpty().MinimumLength(2);
        });
    }
}

public class RecordCashContributionCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    IContributionSettlementService settlementService) : IRequestHandler<RecordCashContributionCommand, CashContributionResult>
{
    public async Task<CashContributionResult> Handle(RecordCashContributionCommand request, CancellationToken cancellationToken)
    {
        var collectorAuth0Id = request.ExplicitUserId ?? currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(collectorAuth0Id))
        {
            throw new UnauthorizedAccessException("Collector authentication is required.");
        }

        var collector = await dbContext.Users
            .Include(user => user.ScopeAssignments)
            .FirstOrDefaultAsync(user => user.Auth0Id == collectorAuth0Id, cancellationToken);

        if (collector == null)
        {
            throw new UnauthorizedAccessException("Collector profile not found.");
        }

        if (!collector.IsActive)
        {
            throw new UnauthorizedAccessException("Collector is inactive.");
        }

        var collectorAssignments = collector.ScopeAssignments
            .Where(assignment => assignment.Role == "Collector")
            .ToList();

        var @event = await dbContext.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);
        if (@event == null) throw new KeyNotFoundException("Event not found.");

        var hasEventAccess = collectorAssignments.Any(assignment =>
            assignment.ScopeType == ScopeType.Event && assignment.TargetId == request.EventId);

        var hasBranchAccess = collectorAssignments.Any(assignment =>
            assignment.ScopeType == ScopeType.Branch && assignment.TargetId == @event.BranchId);

        if (!hasEventAccess && !hasBranchAccess)
        {
            throw new UnauthorizedAccessException("Collector is not assigned to this event or branch.");
        }

        var hasFundAccess = collectorAssignments.Any(assignment =>
            assignment.ScopeType == ScopeType.RecipientFund && assignment.TargetId == request.RecipientFundId);

        if (!hasFundAccess && !hasBranchAccess && !hasEventAccess)
        {
            // If they have branch or event access, they might not need direct fund assignment
            // But usually collectors are assigned to funds. I'll stick to the logic: Branch/Event access implies access to children.
        }
        
        // Actually, I'll follow the existing strictness but allow Branch-level access to override Event/Fund requirements
        if (!hasFundAccess && !hasEventAccess && !hasBranchAccess)
        {
             throw new UnauthorizedAccessException("Collector is not assigned to this recipient fund.");
        }

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
            Name = request.Anonymous ? "Anonymous" : request.ContributorName?.Trim() ?? string.Empty,
            Email = request.ContributorEmail?.Trim() ?? string.Empty,
            PhoneNumber = request.ContributorPhone.Trim(),
            IsAnonymous = request.Anonymous,
        };

        dbContext.Contributors.Add(contributor);

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
            ContributorName = request.Anonymous ? "Anonymous" : request.ContributorName?.Trim() ?? string.Empty,
            Method = request.PaymentMethod,
            Status = ContributionStatus.RecordedCash,
            Note = request.Note
        };

        dbContext.Contributions.Add(contribution);
        var settlement = await settlementService.SettleContributionAsync(contribution, null, collector.Id, cancellationToken);
        
        // Ensure the receipt also gets the BranchId if possible
        if (contribution.Receipt != null)
        {
            contribution.Receipt.BranchId = @event.BranchId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CashContributionResult(contribution.Id, settlement.ReceiptId, contribution.Status.ToString());
    }
}
