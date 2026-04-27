using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Users.Queries.GetMe;

public record GetMeQuery : IRequest<UserDetailDto>;

public class GetMeQueryHandler(IApplicationDbContext dbContext, ICurrentUserService currentUserService) : IRequestHandler<GetMeQuery, UserDetailDto>
{
    public async Task<UserDetailDto> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var auth0Id = currentUserService.UserId;
        if (string.IsNullOrEmpty(auth0Id))
        {
            throw new UnauthorizedAccessException("Not authenticated.");
        }

        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, cancellationToken);

        if (user == null)
        {
            throw new KeyNotFoundException("User identity not found in local matrix.");
        }

        var scopeDtos = new List<ScopeAssignmentDto>();
        foreach (var a in user.ScopeAssignments)
        {
            string? targetName = null;
            if (a.ScopeType == Domain.Entities.ScopeType.Event && a.TargetId.HasValue)
            {
                targetName = await dbContext.Events.Where(e => e.Id == a.TargetId).Select(e => e.Title).FirstOrDefaultAsync(cancellationToken);
            }
            else if (a.ScopeType == Domain.Entities.ScopeType.RecipientFund && a.TargetId.HasValue)
            {
                targetName = await dbContext.RecipientFunds.Where(f => f.Id == a.TargetId).Select(f => f.Name).FirstOrDefaultAsync(cancellationToken);
            }
            else if (a.ScopeType == Domain.Entities.ScopeType.Branch && a.TargetId.HasValue)
            {
                targetName = await dbContext.Branches.Where(b => b.Id == a.TargetId).Select(b => b.Name).FirstOrDefaultAsync(cancellationToken);
            }
            else if (a.ScopeType == Domain.Entities.ScopeType.Organisation && a.TargetId.HasValue)
            {
                targetName = await dbContext.Tenants.Where(t => t.Id == a.TargetId).Select(t => t.Name).FirstOrDefaultAsync(cancellationToken);
            }

            scopeDtos.Add(new ScopeAssignmentDto(
                a.Id,
                a.Role,
                a.ScopeType.ToString(),
                a.TargetId,
                targetName));
        }

        return new UserDetailDto(
            user.Id,
            user.Auth0Id,
            user.Email,
            user.Name,
            user.PhoneNumber,
            user.IsSuspended ? "Suspended" : (user.IsActive ? "Active" : "Inactive"),
            user.InviteStatus.ToString(),
            user.CreatedAt,
            user.LastLoginAt,
            user.IsPlatformAdmin,
            scopeDtos,
            currentUserService.Roles);
    }
}
