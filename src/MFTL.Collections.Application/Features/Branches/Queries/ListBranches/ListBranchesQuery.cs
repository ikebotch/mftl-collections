using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Application.Features.Branches.Queries.ListBranches;

public record ListBranchesQuery(Guid? TenantId = null) : IRequest<IEnumerable<BranchDto>>;

public record BranchDto(Guid Id, string Name, string Identifier, Guid TenantId, string? Location = null, bool IsActive = true);

public class ListBranchesQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<ListBranchesQuery, IEnumerable<BranchDto>>
{
    public async Task<IEnumerable<BranchDto>> Handle(ListBranchesQuery request, CancellationToken cancellationToken)
    {
        var auth0Id = currentUserService.UserId;
        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, cancellationToken);

        if (user == null) return Enumerable.Empty<BranchDto>();

        var query = dbContext.Branches.AsQueryable();
        
        if (request.TenantId.HasValue)
        {
            query = query.Where(b => b.TenantId == request.TenantId.Value);
        }

        if (!user.IsPlatformAdmin)
        {
            var assignedScopes = user.ScopeAssignments.ToList();
            
            // Check if user is an Org Admin for the requested tenant context
            bool isOrgAdmin = assignedScopes.Any(s => 
                s.ScopeType == Domain.Entities.ScopeType.Organisation && 
                (!request.TenantId.HasValue || s.TargetId == request.TenantId.Value));

            if (!isOrgAdmin)
            {
                // Otherwise, filter to specifically assigned branches
                var assignedBranchIds = assignedScopes
                    .Where(s => s.ScopeType == Domain.Entities.ScopeType.Branch && s.TargetId.HasValue)
                    .Select(s => s.TargetId!.Value)
                    .ToList();
                
                query = query.Where(b => assignedBranchIds.Contains(b.Id));
            }
        }

        return await query
            .Select(b => new BranchDto(b.Id, b.Name, b.Identifier, b.TenantId, b.Location, b.IsActive))
            .ToListAsync(cancellationToken);
    }
}
