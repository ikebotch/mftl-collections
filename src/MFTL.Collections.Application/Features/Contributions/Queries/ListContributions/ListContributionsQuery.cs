using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Contributions.Queries.ListContributions;

public record ListContributionsQuery() : IRequest<IEnumerable<ContributionDto>>;

public class ListContributionsQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListContributionsQuery, IEnumerable<ContributionDto>>
{
    public async Task<IEnumerable<ContributionDto>> Handle(ListContributionsQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Contributions
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ContributionDto(
                c.Id,
                c.EventId,
                c.RecipientFundId,
                c.Amount,
                c.Currency,
                c.ContributorName,
                c.Method,
                c.Status.ToString(),
                c.PaymentId,
                c.Receipt != null ? (Guid?)c.Receipt.Id : null,
                c.Note))
            .ToListAsync(cancellationToken);
    }
}
