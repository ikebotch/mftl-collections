using MFTL.Collections.Application.Common.Interfaces;
using MediatR;
using MFTL.Collections.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Contributions.Queries.GetContributionById;

public record GetContributionByIdQuery(Guid Id) : IRequest<ContributionDto>;

public class GetContributionByIdQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetContributionByIdQuery, ContributionDto>
{
    public async Task<ContributionDto> Handle(GetContributionByIdQuery request, CancellationToken cancellationToken)
    {
        var contribution = await dbContext.Contributions
            .Include(c => c.Receipt)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (contribution == null)
        {
            throw new KeyNotFoundException("Contribution not found.");
        }

        return new ContributionDto(
            contribution.Id,
            contribution.EventId,
            contribution.RecipientFundId,
            contribution.Amount,
            contribution.Currency,
            contribution.IsAnonymous ? "Anonymous" : contribution.ContributorName,
            contribution.Method,
            contribution.Status.ToString(),
            contribution.PaymentId,
            contribution.Receipt?.Id,
            contribution.Note);
    }
}
