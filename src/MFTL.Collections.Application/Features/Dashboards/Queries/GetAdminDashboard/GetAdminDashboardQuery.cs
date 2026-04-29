using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Features.Dashboards.Queries.GetAdminDashboard;

public record GetAdminDashboardQuery(
    IEnumerable<Guid>? BranchIds = null,
    IEnumerable<Guid>? TenantIds = null) : IRequest<AdminDashboardDto>;

public class GetAdminDashboardHandler(
    IApplicationDbContext dbContext, 
    IAuth0Service auth0Service,
    IAccessPolicyResolver policyResolver) : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    public async Task<AdminDashboardDto> Handle(GetAdminDashboardQuery request, CancellationToken cancellationToken)
    {
        var isAuth0Configured = await auth0Service.IsConfiguredAsync();
        var policy = await policyResolver.ResolvePolicyAsync();

        var totalEventsQuery = policy.FilterEvents(dbContext.Events);
        if (request.TenantIds != null && request.TenantIds.Any())
            totalEventsQuery = totalEventsQuery.Where(e => request.TenantIds.Contains(e.TenantId));
        if (request.BranchIds != null && request.BranchIds.Any())
            totalEventsQuery = totalEventsQuery.Where(e => request.BranchIds.Contains(e.BranchId));

        var totalEvents = await totalEventsQuery.CountAsync(cancellationToken);
        
        var contributionsQuery = policy.FilterCollections(dbContext.Contributions)
            .Where(c => c.Status == ContributionStatus.Completed);
        
        if (request.TenantIds != null && request.TenantIds.Any())
            contributionsQuery = contributionsQuery.Where(c => request.TenantIds.Contains(c.TenantId));
        if (request.BranchIds != null && request.BranchIds.Any())
            contributionsQuery = contributionsQuery.Where(c => request.BranchIds.Contains(c.BranchId));

        var contributions = await contributionsQuery.ToListAsync(cancellationToken);

        var totalContributions = contributions.Count;
        
        var totals = contributions
            .GroupBy(c => c.Currency)
            .Select(g => new CurrencyTotalDto(g.Key, g.Sum(c => c.Amount)))
            .ToList();

        var fundsQuery = policy.FilterFunds(dbContext.RecipientFunds);
        if (request.TenantIds != null && request.TenantIds.Any())
            fundsQuery = fundsQuery.Where(f => request.TenantIds.Contains(f.TenantId));
        if (request.BranchIds != null && request.BranchIds.Any())
            fundsQuery = fundsQuery.Where(f => request.BranchIds.Contains(f.BranchId));

        var activeRecipientFunds = await fundsQuery.CountAsync(cancellationToken);
        
        // Donors derived from unique contributors
        var donorsQuery = dbContext.Contributors.AsQueryable();
        if (request.TenantIds != null && request.TenantIds.Any())
            donorsQuery = donorsQuery.Where(d => request.TenantIds.Contains(d.TenantId));
        if (request.BranchIds != null && request.BranchIds.Any())
            donorsQuery = donorsQuery.Where(d => request.BranchIds.Contains(d.BranchId));
        
        var totalDonors = await donorsQuery.CountAsync(cancellationToken);
        
        var receiptsQuery = dbContext.Receipts.AsQueryable();
        if (request.TenantIds != null && request.TenantIds.Any())
            receiptsQuery = receiptsQuery.Where(r => request.TenantIds.Contains(r.TenantId));
        if (request.BranchIds != null && request.BranchIds.Any())
            receiptsQuery = receiptsQuery.Where(r => request.BranchIds.Contains(r.BranchId));

        var totalReceipts = await receiptsQuery.CountAsync(cancellationToken);
        
        var totalCollectors = await dbContext.UserScopeAssignments
            .Where(a => a.Role == "Collector")
            .Select(a => a.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var recentContributions = await contributionsQuery
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
            IsAuth0Configured: isAuth0Configured,
            IsAuth0WebhookConfigured: await auth0Service.IsWebhookConfiguredAsync()
        );
    }
}
