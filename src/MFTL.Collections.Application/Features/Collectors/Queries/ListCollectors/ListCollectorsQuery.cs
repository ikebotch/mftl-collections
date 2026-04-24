using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorMe;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Features.Collectors.Queries.ListCollectors;

public record ListCollectorsQuery() : IRequest<IEnumerable<CollectorMeDto>>;

public class ListCollectorsQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListCollectorsQuery, IEnumerable<CollectorMeDto>>
{
    public async Task<IEnumerable<CollectorMeDto>> Handle(ListCollectorsQuery request, CancellationToken cancellationToken)
    {
        var collectors = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .Where(u => u.ScopeAssignments.Any(a => a.Role == "Collector"))
            .ToListAsync(cancellationToken);

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
                receiptsToday.Sum(r => r.Contribution.Amount),
                receiptsToday.Count,
                lastActive,
                hasAssignments,
                user.IsActive
                    ? (hasAssignments ? null : "No assignments")
                    : "Inactive"));
        }

        return results;
    }
}
