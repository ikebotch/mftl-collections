using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Common;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Features.Events.Queries.GetEventBySlug;

public record GetEventBySlugQuery(string Slug) : IRequest<EventDto>;

public class GetEventBySlugQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetEventBySlugQuery, EventDto>
{
    public async Task<EventDto> Handle(GetEventBySlugQuery request, CancellationToken cancellationToken)
    {
        var e = await dbContext.Events
            .IgnoreQueryFilters()
            .Include(e => e.RecipientFunds)
            .FirstOrDefaultAsync(x => x.Slug == request.Slug, cancellationToken);

        if (e == null) throw new KeyNotFoundException($"Event with slug '{request.Slug}' not found.");

        var eventContributions = await dbContext.Contributions
            .IgnoreQueryFilters()
            .Where(c => c.EventId == e.Id && c.Status == ContributionStatus.Completed)
            .ToListAsync(cancellationToken);

        var totals = eventContributions
            .GroupBy(c => c.Currency)
            .Select(g => new CurrencyTotalDto(g.Key, g.Sum(c => c.Amount)))
            .ToList();

        var collectorCount = await dbContext.UserScopeAssignments
            .IgnoreQueryFilters()
            .CountAsync(a => a.ScopeType == Domain.Entities.ScopeType.Event && a.TargetId == e.Id && a.Role == "Collector", cancellationToken);

        return new EventDto(
            e.Id,
            e.Title,
            e.Description,
            e.EventDate,
            e.IsActive,
            totals,
            e.RecipientFunds.Sum(f => f.TargetAmount),
            e.RecipientFunds.Count,
            collectorCount,
            e.Slug,
            e.DisplayImageUrl,
            e.ReceiptLogoUrl);
    }
}
