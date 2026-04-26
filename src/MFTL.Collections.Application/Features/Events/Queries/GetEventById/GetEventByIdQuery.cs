using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Common;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Features.Events.Queries.GetEventById;

public record GetEventByIdQuery(Guid Id) : IRequest<EventDto>;

public class GetEventByIdQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetEventByIdQuery, EventDto>
{
    public async Task<EventDto> Handle(GetEventByIdQuery request, CancellationToken cancellationToken)
    {
        var e = await dbContext.Events
            .Include(e => e.RecipientFunds)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (e == null) throw new KeyNotFoundException("Event not found.");

        var eventContributions = await dbContext.Contributions
            .Where(c => c.EventId == e.Id && c.Status == ContributionStatus.Completed)
            .ToListAsync(cancellationToken);

        var totals = eventContributions
            .GroupBy(c => c.Currency)
            .Select(g => new CurrencyTotalDto(g.Key, g.Sum(c => c.Amount)))
            .ToList();

        var collectorCount = await dbContext.UserScopeAssignments
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
            e.ReceiptLogoUrl,
            e.BranchId);
    }
}
