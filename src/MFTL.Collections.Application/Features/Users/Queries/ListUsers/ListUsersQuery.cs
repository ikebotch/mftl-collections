using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Users.Queries.ListUsers;

public record ListUsersQuery() : IRequest<IEnumerable<UserDto>>;

public class ListUsersHandler(IApplicationDbContext dbContext) : IRequestHandler<ListUsersQuery, IEnumerable<UserDto>>
{
    public async Task<IEnumerable<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .ToListAsync(cancellationToken);

        return users.Select(u => new UserDto(
            u.Id,
            u.Name,
            u.Email,
            u.ScopeAssignments.FirstOrDefault()?.Role ?? (u.IsPlatformAdmin ? "Platform Admin" : "User"),
            u.IsActive ? "Active" : "Inactive",
            "Accepted", // Default for existing users in this simple flow
            u.ScopeAssignments.FirstOrDefault()?.ScopeType.ToString() ?? "Global"
        ));
    }
}
