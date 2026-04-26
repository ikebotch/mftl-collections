using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Features.Users.Queries.ListUsers;

public record ListUsersQuery() : IRequest<IEnumerable<UserDto>>;

public class ListUsersHandler(IApplicationDbContext dbContext, IBranchContext branchContext) : IRequestHandler<ListUsersQuery, IEnumerable<UserDto>>
{
    public async Task<IEnumerable<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Users
            .Include(u => u.ScopeAssignments)
            .AsQueryable();

        if (branchContext.BranchIds.Count > 0)
        {
            query = query.Where(u => u.ScopeAssignments.Any(a => 
                (a.ScopeType == ScopeType.Branch && a.TargetId.HasValue && branchContext.BranchIds.Contains(a.TargetId.Value)) ||
                (a.ScopeType == ScopeType.Organisation) ||
                (a.ScopeType == ScopeType.Platform) ||
                u.IsPlatformAdmin));
        }

        var users = await query.ToListAsync(cancellationToken);

        return users.Select(u => new UserDto(
            u.Id,
            u.Name,
            u.Email,
            u.ScopeAssignments.FirstOrDefault()?.Role ?? (u.IsPlatformAdmin ? "Platform Admin" : "User"),
            u.IsSuspended ? "Suspended" : (u.IsActive ? "Active" : "Inactive"),
            u.InviteStatus.ToString(),
            u.ScopeAssignments.FirstOrDefault()?.ScopeType.ToString() ?? "Global",
            u.LastLoginAt,
            u.IsPlatformAdmin
        ));
    }
}
