using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Contributions.Queries.ListContributions;

public record ListContributionsQuery() : IRequest<IEnumerable<ContributionListItemDto>>;

public class ListContributionsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ListContributionsQuery, IEnumerable<ContributionListItemDto>>
{
    public async Task<IEnumerable<ContributionListItemDto>> Handle(
        ListContributionsQuery request,
        CancellationToken cancellationToken)
    {
        return await dbContext.Contributions
            .Include(contribution => contribution.Event)
            .Include(contribution => contribution.RecipientFund)
            .OrderByDescending(contribution => contribution.CreatedAt)
            .Select(contribution => new ContributionListItemDto(
                contribution.Id,
                contribution.CreatedAt,
                contribution.Event.Title,
                contribution.RecipientFund.Name,
                contribution.Method,
                contribution.Status.ToString(),
                contribution.Amount,
                contribution.Currency))
            .ToListAsync(cancellationToken);
    }
}
