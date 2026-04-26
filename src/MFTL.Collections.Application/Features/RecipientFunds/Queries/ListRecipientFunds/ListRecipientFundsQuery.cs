using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Common;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Features.RecipientFunds.Queries.ListRecipientFunds;

public record ListRecipientFundsQuery() : IRequest<IEnumerable<RecipientFundDto>>;

public class ListRecipientFundsQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListRecipientFundsQuery, IEnumerable<RecipientFundDto>>
{
    public async Task<IEnumerable<RecipientFundDto>> Handle(ListRecipientFundsQuery request, CancellationToken cancellationToken)
    {
        var funds = await dbContext.RecipientFunds.ToListAsync(cancellationToken);
        var fundIds = funds.Select(f => f.Id).ToList();

        var contributions = await dbContext.Contributions
            .Where(c => fundIds.Contains(c.RecipientFundId) && c.Status == ContributionStatus.Completed)
            .ToListAsync(cancellationToken);

        return funds.Select(f => 
        {
            var fundContributions = contributions.Where(c => c.RecipientFundId == f.Id);
            var totals = fundContributions
                .GroupBy(c => c.Currency)
                .Select(g => new CurrencyTotalDto(g.Key, g.Sum(c => c.Amount)))
                .ToList();

            return new RecipientFundDto(
                f.Id,
                f.EventId,
                f.Name,
                f.Description,
                f.TargetAmount,
                f.IsActive,
                totals);
        });
    }
}
