using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.RecipientFunds.Commands.UpdateRecipientFund;

public record UpdateRecipientFundCommand(Guid Id, string Name, string? Description, decimal TargetAmount, string? Metadata) : IRequest<bool>;

public class UpdateRecipientFundCommandValidator : AbstractValidator<UpdateRecipientFundCommand>
{
    public UpdateRecipientFundCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
    }
}

public class UpdateRecipientFundCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<UpdateRecipientFundCommand, bool>
{
    public async Task<bool> Handle(UpdateRecipientFundCommand request, CancellationToken cancellationToken)
    {
        var fund = await dbContext.RecipientFunds
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (fund == null) return false;

        fund.Name = request.Name;
        fund.Description = request.Description ?? string.Empty;
        fund.TargetAmount = request.TargetAmount;
        fund.Metadata = request.Metadata;

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
