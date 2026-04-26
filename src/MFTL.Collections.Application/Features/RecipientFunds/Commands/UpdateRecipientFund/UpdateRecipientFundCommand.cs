using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.RecipientFunds.Commands.UpdateRecipientFund;

public record UpdateRecipientFundCommand(Guid Id, string Name, string? Description, decimal TargetAmount, bool IsActive, string? Metadata) : IRequest<bool>;

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

        var @event = await dbContext.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == fund.EventId, cancellationToken);
        
        fund.Name = request.Name;
        fund.Description = request.Description ?? string.Empty;
        fund.TargetAmount = request.TargetAmount;
        fund.IsActive = request.IsActive;
        fund.Metadata = request.Metadata;
        
        if (@event != null)
        {
            fund.BranchId = @event.BranchId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
