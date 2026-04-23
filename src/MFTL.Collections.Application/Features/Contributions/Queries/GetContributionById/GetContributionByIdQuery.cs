using MFTL.Collections.Application.Common.Interfaces;
using MediatR;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Contributions.Queries.GetContributionById;

public record GetContributionByIdQuery(Guid Id) : IRequest<Contribution?>;

public class GetContributionByIdQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetContributionByIdQuery, Contribution?>
{
    public async Task<Contribution?> Handle(GetContributionByIdQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Contributions
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
    }
}
