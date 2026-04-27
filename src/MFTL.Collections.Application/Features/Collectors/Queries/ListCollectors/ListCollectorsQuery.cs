using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorMe;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Features.Collectors.Queries.ListCollectors;

public record ListCollectorsQuery(
    Guid? EventId = null,
    IEnumerable<Guid>? BranchIds = null,
    IEnumerable<Guid>? TenantIds = null) : IRequest<IEnumerable<CollectorMeDto>>;

public class ListCollectorsQueryHandler(IApplicationDbContext dbContext, IBranchContext branchContext, ITenantContext tenantContext) : IRequestHandler<ListCollectorsQuery, IEnumerable<CollectorMeDto>>
{
    public async Task<IEnumerable<CollectorMeDto>> Handle(ListCollectorsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Users
            .Include(u => u.ScopeAssignments)
            .Where(u => u.ScopeAssignments.Any(a => a.Role == "Collector"));

        var effectiveBranchIds = request.BranchIds ?? branchContext.BranchIds;
        var effectiveTenantIds = request.TenantIds ?? tenantContext.TenantIds;

        if (effectiveBranchIds.Any())
        {
            query = query.Where(u => u.ScopeAssignments.Any(a => 
                a.ScopeType == ScopeType.Branch && a.TargetId.HasValue && effectiveBranchIds.Contains(a.TargetId.Value)));
        }
        else if (effectiveTenantIds.Any())
        {
            query = query.Where(u => u.ScopeAssignments.Any(a => 
                (a.ScopeType == ScopeType.Organisation && effectiveTenantIds.Contains(a.TargetId ?? Guid.Empty)) ||
                (a.ScopeType == ScopeType.Branch && dbContext.Branches.Any(b => b.Id == a.TargetId && effectiveTenantIds.Contains(b.TenantId)))
            ));
        }
        
        if (request.EventId.HasValue)
        {
            query = query.Where(u => u.ScopeAssignments.Any(a => 
                (a.ScopeType == ScopeType.Event && a.TargetId == request.EventId.Value) ||
                (a.ScopeType == ScopeType.RecipientFund && dbContext.RecipientFunds.Any(rf => rf.Id == a.TargetId && rf.EventId == request.EventId.Value))
            ));
        }

        var collectors = await query.ToListAsync(cancellationToken);

        var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        
        var results = new List<CollectorMeDto>();

        foreach (var user in collectors)
        {
            var assignments = user.ScopeAssignments.Where(a => a.Role == "Collector").ToList();
            var eventCount = assignments.Count(a => a.ScopeType == ScopeType.Event);
            var fundCount = assignments.Count(a => a.ScopeType == ScopeType.RecipientFund);
            var hasAssignments = eventCount > 0 && fundCount > 0;

            var receiptsToday = await dbContext.Receipts
                .Where(r => r.RecordedByUserId == user.Id && r.IssuedAt >= today)
                .Include(r => r.Contribution)
                .ToListAsync(cancellationToken);

            var lastActive = await dbContext.Receipts
                .Where(r => r.RecordedByUserId == user.Id)
                .OrderByDescending(r => r.IssuedAt)
                .Select(r => (DateTimeOffset?)r.IssuedAt)
                .FirstOrDefaultAsync(cancellationToken);

            results.Add(new CollectorMeDto(
                user.Id,
                user.Name,
                user.Email,
                user.IsActive ? "Active" : "Inactive",
                eventCount,
                fundCount,
                receiptsToday.Sum(r => r.Contribution?.Amount ?? 0),
                receiptsToday.Count,
                lastActive,
                hasAssignments,
                user.IsActive
                    ? (hasAssignments ? null : "No assignments")
                    : "Inactive",
                user.PhoneNumber,
                assignments.Where(a => a.ScopeType == ScopeType.Event).Select(a => a.TargetId ?? Guid.Empty)));
        }

        return results;
    }
}
