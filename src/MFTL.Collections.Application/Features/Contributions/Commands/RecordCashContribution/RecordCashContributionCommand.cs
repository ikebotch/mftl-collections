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
    string? Reference,
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
        // Identity MUST come from the authenticated user.
        // ExplicitUserId fallback removed for security.
        var collectorAuth0Id = currentUserService.UserId;
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

        // Scope Enforcement:
        // 1. Direct event assignment
        var hasEventAccess = collectorAssignments.Any(assignment =>
            assignment.ScopeType == ScopeType.Event && assignment.TargetId == request.EventId);

        // 2. Branch assignment (covers all events in branch)
        var hasBranchAccess = collectorAssignments.Any(assignment =>
            assignment.ScopeType == ScopeType.Branch && assignment.TargetId == @event.BranchId);

        // 3. Fund assignment (covers only that fund)
        var hasFundAccess = collectorAssignments.Any(assignment =>
            assignment.ScopeType == ScopeType.RecipientFund && assignment.TargetId == request.RecipientFundId);

        // A collector can record if they have Branch access, Event access, OR specific Fund access.
        if (!hasEventAccess && !hasBranchAccess && !hasFundAccess)
        {
            throw new UnauthorizedAccessException("Collector is not assigned to this event, branch, or recipient fund.");
        }

        // Verify the fund belongs to the event
        var recipientFund = await dbContext.RecipientFunds
            .FirstOrDefaultAsync(f => f.Id == request.RecipientFundId && f.EventId == request.EventId, cancellationToken);

        if (recipientFund == null)
        {
            throw new KeyNotFoundException("Recipient fund not found in the specified event.");
        }

        var contributor = new Contributor
        {
            TenantId = recipientFund.TenantId,
            BranchId = @event.BranchId,
            Name = request.Anonymous ? "Anonymous" : request.ContributorName?.Trim() ?? string.Empty,
            Email = request.ContributorEmail?.Trim() ?? string.Empty,
            PhoneNumber = request.ContributorPhone.Trim(),
            IsAnonymous = request.Anonymous,
        };

        dbContext.Contributors.Add(contributor);

        var contribution = new Contribution
        {
            TenantId = recipientFund.TenantId,
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
            Note = request.Note,
            Reference = request.Reference
        };

        dbContext.Contributions.Add(contribution);
        var settlement = await settlementService.SettleContributionAsync(contribution, null, collector.Id, cancellationToken);
        
        if (contribution.Receipt != null)
        {
            contribution.Receipt.BranchId = @event.BranchId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CashContributionResult(contribution.Id, settlement.ReceiptId, contribution.Status.ToString());
    }
}
