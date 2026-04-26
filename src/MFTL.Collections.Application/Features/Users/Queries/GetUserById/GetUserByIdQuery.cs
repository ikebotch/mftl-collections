using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Users.Queries.GetUserById;

public record GetUserByIdQuery(Guid Id) : IRequest<UserDto>;

public class GetUserByIdQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetUserByIdQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        return new UserDto(
            user.Id,
            user.Auth0Id,
            user.Email,
            user.Name,
            user.PhoneNumber,
            user.IsActive ? "Active" : "Inactive",
            user.CreatedAt,
            user.ScopeAssignments.Select(a => new ScopeAssignmentDto(
                a.Id,
                a.Role,
                a.ScopeType.ToString(),
                a.TargetId,
                a.IsActive
            ))
        );
    }
}
