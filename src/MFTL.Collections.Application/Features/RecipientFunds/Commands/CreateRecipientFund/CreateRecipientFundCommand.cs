using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Domain.Entities;
using FluentValidation;

namespace MFTL.Collections.Application.Features.RecipientFunds.Commands.CreateRecipientFund;

public record CreateRecipientFundCommand(Guid EventId, string Name, string? Description, decimal TargetAmount, bool IsActive, string? Metadata) : IRequest<Guid>;

public class CreateRecipientFundCommandValidator : AbstractValidator<CreateRecipientFundCommand>
{
    public CreateRecipientFundCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.EventId).NotEmpty();
    }
}

public class CreateRecipientFundCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<CreateRecipientFundCommand, Guid>
{
    public async Task<Guid> Handle(CreateRecipientFundCommand request, CancellationToken cancellationToken)
    {
        var fund = new RecipientFund
        {
            EventId = request.EventId,
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            TargetAmount = request.TargetAmount,
            IsActive = request.IsActive,
            Metadata = request.Metadata
        };

        dbContext.RecipientFunds.Add(fund);
        await dbContext.SaveChangesAsync(cancellationToken);

        return fund.Id;
    }
}
