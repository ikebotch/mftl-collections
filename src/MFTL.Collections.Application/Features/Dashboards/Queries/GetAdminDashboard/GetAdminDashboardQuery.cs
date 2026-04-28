using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Features.Dashboards.Queries.GetAdminDashboard;

public record GetAdminDashboardQuery() : IRequest<AdminDashboardDto>;

public class GetAdminDashboardHandler(IApplicationDbContext dbContext, IAuth0Service auth0Service) : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    public async Task<AdminDashboardDto> Handle(GetAdminDashboardQuery request, CancellationToken cancellationToken)
    {
        var isAuth0Configured = await auth0Service.IsConfiguredAsync();
        var totalEvents = await dbContext.Events.CountAsync(cancellationToken);
        
        var contributions = await dbContext.Contributions
            .Where(c => c.Status == ContributionStatus.Completed)
            .ToListAsync(cancellationToken);

        var totalContributions = contributions.Count;
        
        var totals = contributions
            .GroupBy(c => c.Currency)
            .Select(g => new CurrencyTotalDto(g.Key, g.Sum(c => c.Amount)))
            .ToList();

        var activeRecipientFunds = await dbContext.RecipientFunds.CountAsync(cancellationToken);
        
        // Donors derived from unique contributors
        var totalDonors = await dbContext.Contributors.CountAsync(cancellationToken);
        
        var totalReceipts = await dbContext.Receipts.CountAsync(cancellationToken);
        
        // Collectors are users with the Collector role in scope assignments
        var totalCollectors = await dbContext.UserScopeAssignments
            .Where(a => a.Role == "Collector")
            .Select(a => a.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var recentContributions = await dbContext.Contributions
            .Include(c => c.Event)
            .OrderByDescending(c => c.CreatedAt)
            .Take(10)
            .Select(c => new RecentContributionDto(
                c.ContributorName,
                c.Amount,
                c.Currency,
                c.CreatedAt,
                c.Status.ToString(),
                c.Event.Title,
                c.Method
            ))
            .ToListAsync(cancellationToken);

        return new AdminDashboardDto(
            TotalEvents: totalEvents,
            TotalContributions: totalContributions,
            Totals: totals,
            ActiveRecipientFunds: activeRecipientFunds,
            TotalCollectors: totalCollectors,
            TotalDonors: totalDonors,
            TotalReceipts: totalReceipts,
            RecentContributions: recentContributions,
            IsAuth0Configured: isAuth0Configured
        );
    }
}
