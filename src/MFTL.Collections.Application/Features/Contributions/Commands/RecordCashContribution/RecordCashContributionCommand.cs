using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using FluentValidation;

namespace MFTL.Collections.Application.Features.Contributions.Commands.RecordCashContribution;

public record RecordCashContributionCommand(Guid EventId, Guid RecipientFundId, decimal Amount, string ContributorName, string? Note) : IRequest<Guid>;

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
    IContributionSettlementService settlementService) : IRequestHandler<RecordCashContributionCommand, Guid>
{
    public async Task<Guid> Handle(RecordCashContributionCommand request, CancellationToken cancellationToken)
    {
        var contribution = new Contribution
        {
            EventId = request.EventId,
            RecipientFundId = request.RecipientFundId,
            Amount = request.Amount,
            ContributorName = request.ContributorName,
            Method = "Cash",
            Status = ContributionStatus.RecordedCash,
            Note = request.Note
        };

        dbContext.Contributions.Add(contribution);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Immediate settlement for cash
        await settlementService.SettleContributionAsync(contribution.Id, null, cancellationToken);

        return contribution.Id;
    }
}
