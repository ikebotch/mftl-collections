using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Common;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;

using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Events.Queries.GetEventById;

[HasPermission("events.view")]
public record GetEventByIdQuery(Guid Id) : IRequest<EventDto>, IHasScope
{
    public Guid? GetScopeId() => Id;
}

public class GetEventByIdQueryHandler(
    IApplicationDbContext dbContext,
    IAccessPolicyResolver policyResolver) : IRequestHandler<GetEventByIdQuery, EventDto>
{
    public async Task<EventDto> Handle(GetEventByIdQuery request, CancellationToken cancellationToken)
    {
        var e = await dbContext.Events
            .Include(e => e.Branch)
            .Include(e => e.RecipientFunds)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (e == null) throw new KeyNotFoundException("Event not found.");

        var policy = await policyResolver.ResolvePolicyAsync();
        if (e.IsPrivate && !policy.CanViewPrivateEvent(e.Id))
        {
            throw new UnauthorizedAccessException("You do not have access to this private event.");
        }


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
