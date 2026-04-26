using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorAssignments;

public sealed record CollectorAssignedEventDto(
    Guid Id,
    string Title,
    string Description,
    string Status,
    DateTimeOffset? EventDate,
    string Location,
    int AssignedFundCount);

public sealed record CollectorAssignedFundDto(
    Guid Id,
    Guid EventId,
    string Name,
    string Description,
    decimal TargetAmount,
    decimal CollectedAmount,
    string Currency);

public sealed record CollectorAssignmentsDto(
    bool HasAssignments,
    string? BlockedReason,
    IEnumerable<CollectorAssignedEventDto> Events,
    IEnumerable<CollectorAssignedFundDto> Funds);

public record GetCollectorAssignmentsQuery(string? ExplicitUserId = null) : IRequest<CollectorAssignmentsDto>;

public class GetCollectorAssignmentsQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<GetCollectorAssignmentsQuery, CollectorAssignmentsDto>
{
    public async Task<CollectorAssignmentsDto> Handle(GetCollectorAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var auth0Id = request.ExplicitUserId ?? currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(auth0Id))
        {
            throw new UnauthorizedAccessException("Collector authentication is required.");
        }

        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, cancellationToken);

        if (user == null)
        {
            throw new KeyNotFoundException("Collector profile not found.");
        }

        if (!user.IsActive)
        {
            return new CollectorAssignmentsDto(false, "Collector is inactive.", [], []);
        }

        var collectorAssignments = user.ScopeAssignments.Where(a => a.Role == "Collector").ToList();
        var eventIds = collectorAssignments
            .Where(a => a.ScopeType == ScopeType.Event && a.TargetId.HasValue)
            .Select(a => a.TargetId!.Value)
            .Distinct()
            .ToList();

        var fundIds = collectorAssignments
            .Where(a => a.ScopeType == ScopeType.RecipientFund && a.TargetId.HasValue)
            .Select(a => a.TargetId!.Value)
            .Distinct()
            .ToList();

        if (eventIds.Count == 0 && fundIds.Count == 0)
        {
            return new CollectorAssignmentsDto(
                false,
                "No active campaign or fund assignments established for this collector.",
                [],
                []);
        }

        var funds = await dbContext.RecipientFunds
            .Where(fund => fundIds.Contains(fund.Id) || eventIds.Contains(fund.EventId))
            .ToListAsync(cancellationToken);

        var allowedEventIdsFromFunds = funds.Select(f => f.EventId).Distinct().ToList();
        var allAllowedEventIds = eventIds.Concat(allowedEventIdsFromFunds).Distinct().ToList();

        var events = await dbContext.Events
            .Where(e => allAllowedEventIds.Contains(e.Id))
            .ToListAsync(cancellationToken);

        return new CollectorAssignmentsDto(
            true,
            null,
            events.Select(evt => new CollectorAssignedEventDto(
                evt.Id,
                evt.Title,
                evt.Description,
                evt.IsActive ? "Live" : "Draft",
                evt.EventDate,
                "Main Site",
                funds.Count(fund => fund.EventId == evt.Id))),
            funds.Select(fund => new CollectorAssignedFundDto(
                fund.Id,
                fund.EventId,
                fund.Name,
                fund.Description,
                fund.TargetAmount,
                fund.CollectedAmount,
                "GHS")));
    }
}
