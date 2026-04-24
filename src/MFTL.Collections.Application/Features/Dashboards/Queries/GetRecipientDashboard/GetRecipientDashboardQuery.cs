using MFTL.Collections.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Features.Dashboards.Queries.GetRecipientDashboard;

public record GetRecipientDashboardQuery(Guid RecipientFundId) : IRequest<RecipientDashboardDto>;

public class GetRecipientDashboardQueryHandler(IApplicationDbContext dbContext, IScopeAccessService scopeAccessService) 
    : IRequestHandler<GetRecipientDashboardQuery, RecipientDashboardDto>
{
    public async Task<RecipientDashboardDto> Handle(GetRecipientDashboardQuery request, CancellationToken cancellationToken)
    {
        // Enforce scoped access
        if (!await scopeAccessService.HasAccessToRecipientFundAsync(request.RecipientFundId))
        {
            throw new UnauthorizedAccessException("You do not have access to this recipient fund.");
        }

        var fund = await dbContext.RecipientFunds
            .FirstOrDefaultAsync(f => f.Id == request.RecipientFundId, cancellationToken);

        if (fund == null) throw new KeyNotFoundException("Recipient fund not found.");

        var recentContributions = await dbContext.Contributions
            .Where(c => c.RecipientFundId == request.RecipientFundId && c.Status == ContributionStatus.Completed)
            .OrderByDescending(c => c.CreatedAt)
            .Take(10)
            .Select(c => new RecentContributionDto(
                c.Contributor != null ? (c.Contributor.IsAnonymous ? "Anonymous" : c.Contributor.Name) : "Guest",
                c.Amount,
                c.CreatedAt,
                c.Status.ToString(),
                null,
                null
            ))
            .ToListAsync(cancellationToken);

        var count = await dbContext.Contributions
            .CountAsync(c => c.RecipientFundId == request.RecipientFundId && c.Status == ContributionStatus.Completed, cancellationToken);

        return new RecipientDashboardDto(
            fund.Id,
            fund.Name,
            fund.TargetAmount,
            fund.CollectedAmount,
            fund.TargetAmount > 0 ? (fund.CollectedAmount / fund.TargetAmount) * 100 : 0,
            count,
            recentContributions
        );
    }
}
