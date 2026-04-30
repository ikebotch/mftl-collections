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

public record GetCollectorAssignmentsQuery() : IRequest<CollectorAssignmentsDto>;

public class GetCollectorAssignmentsQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<GetCollectorAssignmentsQuery, CollectorAssignmentsDto>
{
    public async Task<CollectorAssignmentsDto> Handle(GetCollectorAssignmentsQuery request, CancellationToken cancellationToken)
    {
        // Identity MUST come from the authenticated user.
        // ExplicitUserId fallback removed.
        var auth0Id = currentUserService.UserId;
        
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
        
        // Find events assigned directly or via branch
        var eventIds = collectorAssignments
            .Where(a => a.ScopeType == ScopeType.Event && a.TargetId.HasValue)
            .Select(a => a.TargetId!.Value)
            .ToList();

        var branchIds = collectorAssignments
            .Where(a => a.ScopeType == ScopeType.Branch && a.TargetId.HasValue)
            .Select(a => a.TargetId!.Value)
            .ToList();

        var fundIds = collectorAssignments
            .Where(a => a.ScopeType == ScopeType.RecipientFund && a.TargetId.HasValue)
            .Select(a => a.TargetId!.Value)
            .ToList();

        if (eventIds.Count == 0 && fundIds.Count == 0 && branchIds.Count == 0)
        {
            return new CollectorAssignmentsDto(
                false,
                "No active campaign or fund assignments established for this collector.",
                [],
                []);
        }

        // Removed IgnoreQueryFilters() — we rely on proper scoped access.
        // Actually, since this is a collector endpoint, it might be called in platform context.
        // But the user's assignments themselves should define the scope.
        // We filter funds that are directly assigned, OR belong to assigned events, OR belong to assigned branches.
        var fundsQuery = dbContext.RecipientFunds.AsQueryable();
        
        // If they have branch access, they see all events/funds in that branch.
        // If they have event access, they see all funds in that event.
        // If they have fund access, they see only that fund.
        
        var funds = await dbContext.RecipientFunds
            .Include(f => f.Event)
            .Where(f => fundIds.Contains(f.Id) || 
                        eventIds.Contains(f.EventId) || 
                        branchIds.Contains(f.Event.BranchId))
            .ToListAsync(cancellationToken);

        var allAllowedEventIds = funds.Select(f => f.EventId).Distinct().ToList();
        
        // Also include events that might not have funds yet but are directly assigned
        var events = await dbContext.Events
            .Where(e => allAllowedEventIds.Contains(e.Id) || eventIds.Contains(e.Id) || branchIds.Contains(e.BranchId))
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
