using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using Mapster;

namespace MFTL.Collections.Application.Features.RecipientFunds.Queries.ListRecipientFundsByEvent;

public record ListRecipientFundsByEventQuery(Guid EventId) : IRequest<IEnumerable<RecipientFundDto>>;

public class ListRecipientFundsByEventQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListRecipientFundsByEventQuery, IEnumerable<RecipientFundDto>>
{
    public async Task<IEnumerable<RecipientFundDto>> Handle(ListRecipientFundsByEventQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.RecipientFunds
            .Where(f => f.EventId == request.EventId)
            .ProjectToType<RecipientFundDto>()
            .ToListAsync(cancellationToken);
    }
}
