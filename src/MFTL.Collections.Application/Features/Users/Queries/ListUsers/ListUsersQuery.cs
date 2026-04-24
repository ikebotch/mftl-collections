using MediatR;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Users.Queries.ListUsers;

public record ListUsersQuery() : IRequest<IEnumerable<UserDto>>;

public class ListUsersHandler : IRequestHandler<ListUsersQuery, IEnumerable<UserDto>>
{
    public Task<IEnumerable<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<UserDto>>(new List<UserDto>
        {
            new(Guid.NewGuid(), "Isaac Botchway", "admin@mftl.com", "Platform Admin", "Active", "Accepted", "Global"),
            new(Guid.NewGuid(), "Samuel Osei", "sam@field.com", "Collector", "Active", "Accepted", "Assigned Events"),
            new(Guid.NewGuid(), "Grace Mensah", "grace@super.com", "Supervisor", "Active", "Pending", "Regional")
        });
    }
}
