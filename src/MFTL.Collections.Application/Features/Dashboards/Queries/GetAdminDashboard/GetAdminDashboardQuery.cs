using MediatR;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Dashboards.Queries.GetAdminDashboard;

public record GetAdminDashboardQuery() : IRequest<AdminDashboardDto>;

public class GetAdminDashboardHandler : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    // In a real app, this would use a repository or a projection table.
    // For now, we return mock data or aggregate from existing services.
    public Task<AdminDashboardDto> Handle(GetAdminDashboardQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AdminDashboardDto(
            TotalEvents: 5,
            TotalContributions: 152,
            TotalCollected: 24582.50m,
            ActiveRecipientFunds: 12,
            TotalCollectors: 23,
            TotalDonors: 145,
            TotalReceipts: 150,
            RecentContributions: new List<RecentContributionDto>
            {
                new("John Doe", 100.00m, DateTimeOffset.UtcNow.AddMinutes(-5), "Success", "Youth Conference 2026", "Mobile Money"),
                new("Jane Smith", 250.00m, DateTimeOffset.UtcNow.AddMinutes(-25), "Success", "Missions Outreach", "Card"),
                new("Anonymous", 50.00m, DateTimeOffset.UtcNow.AddHours(-2), "Success", "Easter Retreat", "Cash")
            }
        ));
    }
}
