using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Branches.Queries.ListBranches;

namespace MFTL.Collections.Application.Features.Branches.Queries.GetBranchById;

public record GetBranchByIdQuery(Guid Id) : IRequest<BranchDto>;

public class GetBranchByIdQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetBranchByIdQuery, BranchDto>
{
    public async Task<BranchDto> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
    {
        var branch = await dbContext.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (branch == null) return null!;

        return new BranchDto(
            branch.Id,
            branch.Name,
            branch.Identifier,
            branch.TenantId,
            branch.Location,
            branch.IsActive);
    }
}
