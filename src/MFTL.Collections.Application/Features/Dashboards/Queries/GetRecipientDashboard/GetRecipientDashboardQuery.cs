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

        var contributionsQuery = dbContext.Contributions
            .Where(c => c.RecipientFundId == request.RecipientFundId && c.Status == ContributionStatus.Completed);

        var recentContributions = await contributionsQuery
            .OrderByDescending(c => c.CreatedAt)
            .Take(10)
            .Select(c => new RecentContributionDto(
                c.Contributor != null ? (c.Contributor.IsAnonymous ? "Anonymous" : c.Contributor.Name) : "Guest",
                c.Amount,
                c.Currency,
                c.CreatedAt,
                c.Status.ToString(),
                null,
                c.Method
            ))
            .ToListAsync(cancellationToken);

        var totals = await contributionsQuery
            .GroupBy(c => c.Currency)
            .Select(g => new CurrencyTotalDto(g.Key, g.Sum(c => c.Amount)))
            .ToListAsync(cancellationToken);

        var count = await contributionsQuery.CountAsync(cancellationToken);

        // Progress is tricky with mixed currencies, usually based on target currency
        // For now, we show 0 or calculate against a primary currency if we had FX
        decimal progress = 0;
        var primaryTotal = totals.FirstOrDefault(t => t.Currency == "GHS")?.Amount ?? 0;
        if (fund.TargetAmount > 0)
        {
            progress = (primaryTotal / fund.TargetAmount) * 100;
        }

        return new RecipientDashboardDto(
            fund.Id,
            fund.Name,
            fund.TargetAmount,
            totals,
            progress,
            count,
            recentContributions
        );
    }
}
