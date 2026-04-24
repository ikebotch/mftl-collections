using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Features.Collectors.Queries.ListCollectors;

public record ListCollectorsQuery() : IRequest<IEnumerable<CollectorDto>>;

public class ListCollectorsQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListCollectorsQuery, IEnumerable<CollectorDto>>
{
    public async Task<IEnumerable<CollectorDto>> Handle(ListCollectorsQuery request, CancellationToken cancellationToken)
    {
        // Collectors are users with "Collector" role in any scope for this tenant
        // Since we are in a tenant-filtered context, we just look at assignments
        
        var collectorUsers = await dbContext.Users
            .Where(u => u.ScopeAssignments.Any(s => s.Role == "Collector"))
            .Include(u => u.ScopeAssignments)
            .ToListAsync(cancellationToken);

        var result = new List<CollectorDto>();

        foreach (var user in collectorUsers)
        {
            var assignments = user.ScopeAssignments.Where(a => a.Role == "Collector").ToList();
            
            // Derive stats
            var eventCount = assignments.Count(a => a.ScopeType == ScopeType.Event);
            var fundCount = assignments.Count(a => a.ScopeType == ScopeType.RecipientFund);

            // Total collected today (placeholder logic, would join with Receipts in real app)
            var totalToday = await dbContext.Receipts
                .Where(r => r.RecordedByUserId == user.Id && r.IssuedAt >= DateTimeOffset.UtcNow.Date)
                .SumAsync(r => r.Contribution.Amount, cancellationToken);

            var totalMonth = await dbContext.Receipts
                .Where(r => r.RecordedByUserId == user.Id && r.IssuedAt >= new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero))
                .SumAsync(r => r.Contribution.Amount, cancellationToken);

            var lastActive = await dbContext.Receipts
                .Where(r => r.RecordedByUserId == user.Id)
                .OrderByDescending(r => r.IssuedAt)
                .Select(r => (DateTimeOffset?)r.IssuedAt)
                .FirstOrDefaultAsync(cancellationToken);

            result.Add(new CollectorDto(
                Id: user.Id,
                Name: user.Name,
                Email: user.Email,
                PhoneNumber: null, // User entity doesn't have phone yet
                Status: user.IsActive ? "Active" : "Inactive",
                AssignedEventCount: eventCount,
                AssignedFundCount: fundCount,
                TotalCollectedToday: totalToday,
                TotalCollectedMonth: totalMonth,
                LastActiveAt: lastActive
            ));
        }

        return result;
    }
}
