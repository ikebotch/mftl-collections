using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using Microsoft.EntityFrameworkCore;
using Mapster;

namespace MFTL.Collections.Application.Features.RecipientFunds.Queries.GetRecipientFundById;

public record GetRecipientFundByIdQuery(Guid Id) : IRequest<RecipientFundDto?>;

public class GetRecipientFundByIdQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetRecipientFundByIdQuery, RecipientFundDto?>
{
    public async Task<RecipientFundDto?> Handle(GetRecipientFundByIdQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.RecipientFunds
            .Where(x => x.Id == request.Id)
            .ProjectToType<RecipientFundDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
