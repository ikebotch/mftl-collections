using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Contributions.Commands.UpdateContribution;

public record UpdateContributionCommand : IRequest<bool>
{
    public Guid Id { get; init; }
    public decimal Amount { get; init; }
    public string? ContributorName { get; init; }
    public string? Note { get; init; }
    public string? Reference { get; init; }
    public string? Status { get; init; }
}

public class UpdateContributionCommandValidator : AbstractValidator<UpdateContributionCommand>
{
    public UpdateContributionCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Amount).GreaterThan(0);
        RuleFor(v => v.ContributorName).NotEmpty().MinimumLength(2);
    }
}

public class UpdateContributionCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<UpdateContributionCommand, bool>
{
    public async Task<bool> Handle(UpdateContributionCommand request, CancellationToken cancellationToken)
    {
        var contribution = await dbContext.Contributions
            .Include(c => c.Contributor)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (contribution == null)
        {
            return false;
        }

        contribution.Amount = request.Amount;
        contribution.ContributorName = request.ContributorName ?? contribution.ContributorName;
        contribution.Note = request.Note;
        contribution.Reference = request.Reference;
        
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ContributionStatus>(request.Status, true, out var status))
        {
            contribution.Status = status;
        }

        if (contribution.Contributor != null)
        {
            contribution.Contributor.Name = contribution.ContributorName;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
