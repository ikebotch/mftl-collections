using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Application.Features.Contributions.Queries.ListContributions;

public sealed record ContributionListItemDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    string EventTitle,
    string RecipientFundName,
    string PaymentMethod,
    string Status,
    decimal Amount,
    string Currency);

public record ListContributionsQuery() : IRequest<IEnumerable<ContributionListItemDto>>;

public class ListContributionsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ListContributionsQuery, IEnumerable<ContributionListItemDto>>
{
    public async Task<IEnumerable<ContributionListItemDto>> Handle(
        ListContributionsQuery request,
        CancellationToken cancellationToken)
    {
        return await dbContext.Contributions
            .Include(c => c.Event)
            .Include(c => c.RecipientFund)
            .OrderByDescending(c => c.CreatedAt)
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
