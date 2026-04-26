using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Application.Features.Branches.Queries.ListBranches;

public record ListBranchesQuery(Guid? TenantId = null) : IRequest<IEnumerable<BranchDto>>;

public record BranchDto(Guid Id, string Name, string Identifier, Guid TenantId, string? Location = null, bool IsActive = true);

public class ListBranchesQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListBranchesQuery, IEnumerable<BranchDto>>
{
    public async Task<IEnumerable<BranchDto>> Handle(ListBranchesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Branches.AsQueryable();
        
        if (request.TenantId.HasValue)
        {
            query = query.Where(b => b.TenantId == request.TenantId.Value);
        }

        return await query
            .Select(b => new BranchDto(b.Id, b.Name, b.Identifier, b.TenantId, b.Location, b.IsActive))
            .ToListAsync(cancellationToken);
    }
}
