using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using Mapster;

namespace MFTL.Collections.Application.Features.RecipientFunds.Queries.ListRecipientFunds;

public record ListRecipientFundsQuery() : IRequest<IEnumerable<RecipientFundDto>>;

public class ListRecipientFundsQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListRecipientFundsQuery, IEnumerable<RecipientFundDto>>
{
    public async Task<IEnumerable<RecipientFundDto>> Handle(ListRecipientFundsQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.RecipientFunds
            .ProjectToType<RecipientFundDto>()
            .ToListAsync(cancellationToken);
    }
}
