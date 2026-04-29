using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorAssignments;

public sealed record CollectorAssignedEventDto(
    Guid Id,
    string Title,
    string Description,
    string Status,
    DateTimeOffset? EventDate,
    string Location,
    int AssignedFundCount,
    decimal TotalCollectedByCollector,
    IEnumerable<CollectorAssignedFundDto> Funds);

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
    Guid? TenantId = null,
    Guid? BranchId = null);

[HasPermission("events.view")]
public record GetCollectorAssignmentsQuery(string? ExplicitUserId = null) : IRequest<CollectorAssignmentsDto>, IHasScope
{
    public Guid? GetScopeId() => null;
}

public class GetCollectorAssignmentsQueryHandler(
    IApplicationDbContext dbContext,
    IAccessPolicyResolver policyResolver) : IRequestHandler<GetCollectorAssignmentsQuery, CollectorAssignmentsDto>
{
    public async Task<CollectorAssignmentsDto> Handle(GetCollectorAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var context = await policyResolver.GetAccessContextAsync();
        var auth0Id = request.ExplicitUserId ?? context.Auth0Id;
        
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
            return new CollectorAssignmentsDto(false, "Collector is inactive.", [], null, null);
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

        var orgIds = collectorAssignments
            .Where(a => a.ScopeType == ScopeType.Organisation && a.TargetId.HasValue)
            .Select(a => a.TargetId!.Value)
            .Distinct()
            .ToList();

        var branchIds = collectorAssignments
            .Where(a => a.ScopeType == ScopeType.Branch && a.TargetId.HasValue)
            .Select(a => a.TargetId!.Value)
            .Distinct()
            .ToList();

        if (eventIds.Count == 0 && fundIds.Count == 0 && orgIds.Count == 0 && branchIds.Count == 0)
        {
            return new CollectorAssignmentsDto(
                false,
                "No active campaign or fund assignments established for this collector.",
                [],
                null,
                null);
        }

        var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var receiptsToday = await dbContext.Receipts
            .Where(r => r.RecordedByUserId == user.Id && r.IssuedAt >= today)
            .Include(r => r.Contribution)
            .ToListAsync(cancellationToken);

        var funds = await dbContext.RecipientFunds
            .IgnoreQueryFilters()
            .Include(f => f.Event)
            .Where(fund => 
                fundIds.Contains(fund.Id) || 
                eventIds.Contains(fund.EventId) ||
                (fund.TenantId != Guid.Empty && orgIds.Contains(fund.TenantId)) ||
                (fund.BranchId != Guid.Empty && branchIds.Contains(fund.BranchId)))
            .ToListAsync(cancellationToken);

        var allowedEventIdsFromFunds = funds.Select(f => f.EventId).Distinct().ToList();
        var allAllowedEventIds = eventIds.Concat(allowedEventIdsFromFunds).Distinct().ToList();

        var events = await dbContext.Events
            .IgnoreQueryFilters()
            .Include(e => e.Branch)
            .Where(e => 
                allAllowedEventIds.Contains(e.Id) || 
                orgIds.Contains(e.TenantId) || 
                branchIds.Contains(e.BranchId))
            .ToListAsync(cancellationToken);

        var firstEvent = events.FirstOrDefault();
        var tenantId = firstEvent?.TenantId ?? orgIds.FirstOrDefault();
        var branchId = firstEvent?.BranchId ?? branchIds.FirstOrDefault();

        return new CollectorAssignmentsDto(
            true,
            null,
            events.Select(evt => 
            {
                var eventFunds = funds.Where(f => f.EventId == evt.Id).ToList();
                var eventFundIds = eventFunds.Select(f => f.Id).ToList();
                var collectorTotalForEvent = receiptsToday
                    .Where(r => r.EventId == evt.Id)
                    .Sum(r => r.Contribution?.Amount ?? 0);

                return new CollectorAssignedEventDto(
                    evt.Id,
                    evt.Title,
                    evt.Description,
                    evt.IsActive ? "Live" : "Draft",
                    evt.EventDate,
                    evt.Branch?.Location ?? "Main Site",
                    eventFunds.Count,
                    collectorTotalForEvent,
                    eventFunds.Select(fund => new CollectorAssignedFundDto(
                        fund.Id,
                        fund.EventId,
                        fund.Name,
                        fund.Description,
                        fund.TargetAmount,
                        fund.CollectedAmount,
                        fund.Event.Tenant.DefaultCurrency)));
            }),
            tenantId == Guid.Empty ? null : tenantId,
            branchId == Guid.Empty ? null : branchId);
    }
}
