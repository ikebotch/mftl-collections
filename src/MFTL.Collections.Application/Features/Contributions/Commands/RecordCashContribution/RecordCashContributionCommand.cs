using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Contributions.Commands.RecordCashContribution;

public record RecordCashContributionCommand(Guid EventId, Guid RecipientFundId, decimal Amount, string ContributorName, string? Note) : IRequest<CashContributionResult>;

public class RecordCashContributionCommandValidator : AbstractValidator<RecordCashContributionCommand>
{
    public RecordCashContributionCommandValidator()
    {
        RuleFor(v => v.Amount).GreaterThan(0);
        RuleFor(v => v.ContributorName).NotEmpty();
    }
}

public class RecordCashContributionCommandHandler(
    IApplicationDbContext dbContext,
    IContributionSettlementService settlementService) : IRequestHandler<RecordCashContributionCommand, CashContributionResult>
{
    public async Task<CashContributionResult> Handle(RecordCashContributionCommand request, CancellationToken cancellationToken)
    {
        var recipientFund = await dbContext.RecipientFunds
            .FirstOrDefaultAsync(f => f.Id == request.RecipientFundId && f.EventId == request.EventId, cancellationToken);

        if (recipientFund == null)
        {
            throw new KeyNotFoundException("Recipient fund not found.");
        }

        var contribution = new Contribution
        {
            TenantId = recipientFund.TenantId,
            EventId = request.EventId,
            RecipientFundId = request.RecipientFundId,
            RecipientFund = recipientFund,
            Amount = request.Amount,
            ContributorName = request.ContributorName,
            Method = "Cash",
            Status = ContributionStatus.RecordedCash,
            Note = request.Note
        };

        dbContext.Contributions.Add(contribution);
        var settlement = await settlementService.SettleContributionAsync(contribution, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CashContributionResult(contribution.Id, settlement.ReceiptId, contribution.Status.ToString());
    }
}
