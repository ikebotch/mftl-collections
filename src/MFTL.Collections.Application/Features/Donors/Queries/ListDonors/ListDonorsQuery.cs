using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Features.Donors.Queries.ListDonors;

public record ListDonorsQuery() : IRequest<IEnumerable<DonorDto>>;

public class ListDonorsQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListDonorsQuery, IEnumerable<DonorDto>>
{
    public async Task<IEnumerable<DonorDto>> Handle(ListDonorsQuery request, CancellationToken cancellationToken)
    {
        // Derive donor summaries from contributions
        // In a more mature system, we would have a dedicated Donor/Contributor entity with stable IDs.
        // For now, we group by name + email (if available).
        
        var contributions = await dbContext.Contributions
            .Where(c => c.Status == ContributionStatus.Completed)
            .Include(c => c.Contributor)
            .ToListAsync(cancellationToken);

        var donorGroups = contributions
            .GroupBy(c => new { 
                Name = c.ContributorName, 
                Email = c.Contributor?.Email 
            })
            .Select(g => new DonorDto(
                Name: g.Key.Name,
                Email: g.Key.Email,
                PhoneNumber: g.First().Contributor?.PhoneNumber,
                TotalGiven: g.Sum(c => c.Amount),
                ContributionCount: g.Count(),
                LastDonationDate: g.Max(c => c.CreatedAt),
                EventsSupportedCount: g.Select(c => c.EventId).Distinct().Count(),
                PreferredPaymentMethod: g.GroupBy(c => c.Method)
                                         .OrderByDescending(m => m.Count())
                                         .First().Key,
                IsAnonymous: g.All(c => c.Contributor != null && c.Contributor.IsAnonymous)
            ))
            .OrderByDescending(d => d.TotalGiven)
            .ToList();

        return donorGroups;
    }
}
