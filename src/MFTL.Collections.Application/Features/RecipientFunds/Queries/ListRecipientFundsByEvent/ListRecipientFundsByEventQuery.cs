using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Common;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Features.RecipientFunds.Queries.ListRecipientFundsByEvent;

public record ListRecipientFundsByEventQuery(Guid EventId) : IRequest<IEnumerable<RecipientFundDto>>;

/// <summary>
/// Handler for listing recipient funds for a specific event on the public storefront.
/// NOTE: Uses IgnoreQueryFilters() to allow public access to funds regardless of the 
/// current user's tenant context (which is empty for public storefront), but manually 
/// enforces the IsActive check.
/// </summary>
public class ListRecipientFundsByEventQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListRecipientFundsByEventQuery, IEnumerable<RecipientFundDto>>
{
    public async Task<IEnumerable<RecipientFundDto>> Handle(ListRecipientFundsByEventQuery request, CancellationToken cancellationToken)
    {
        // 1. Verify Event is active
        var ev = await dbContext.Events
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == request.EventId && e.IsActive, cancellationToken);

        if (ev == null)
        {
            return Enumerable.Empty<RecipientFundDto>();
        }

        // 2. Fetch active funds for the event
        var funds = await dbContext.RecipientFunds
            .IgnoreQueryFilters()
            .Where(f => f.EventId == request.EventId && f.IsActive)
            .ToListAsync(cancellationToken);

        var fundIds = funds.Select(f => f.Id).ToList();

        var contributions = await dbContext.Contributions
            .IgnoreQueryFilters()
            .Where(c => fundIds.Contains(c.RecipientFundId) && c.Status == ContributionStatus.Completed)
            .ToListAsync(cancellationToken);

        return funds.Select(f => 
        {
            var fundContributions = contributions.Where(c => c.RecipientFundId == f.Id);
            var totals = fundContributions
                .GroupBy(c => c.Currency)
                .Select(g => new CurrencyTotalDto(g.Key, g.Sum(c => c.Amount)))
                .ToList();

            return new RecipientFundDto(
                f.Id,
                f.EventId,
                f.Name,
                f.Description,
                f.TargetAmount,
                f.IsActive,
                totals);
        });
    }
}
