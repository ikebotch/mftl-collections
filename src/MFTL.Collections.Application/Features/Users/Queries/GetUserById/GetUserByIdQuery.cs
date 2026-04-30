using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Users.Queries.GetUserById;

public record GetUserByIdQuery(Guid Id) : IRequest<UserDetailDto>;

public class GetUserByIdQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetUserByIdQuery, UserDetailDto>
{
    public async Task<UserDetailDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found.");
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
            else if (a.ScopeType == Domain.Entities.ScopeType.Tenant && a.TargetId.HasValue)
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

        var effectiveRoles = scopeDtos.Select(s => s.Role).Distinct().ToList();
        if (user.IsPlatformAdmin && !effectiveRoles.Contains("Platform Admin"))
        {
            effectiveRoles.Add("Platform Admin");
        }

        var permissions = new List<string>();
        if (user.IsPlatformAdmin)
        {
            permissions.Add("*");
        }
        else
        {
            // Fetch permissions for the effective roles from the database
            var rolePermissions = await dbContext.RolePermissions
                .Where(rp => effectiveRoles.Contains(rp.RoleName))
                .Select(rp => rp.PermissionKey)
                .ToListAsync(cancellationToken);

            permissions.AddRange(rolePermissions);
        }
        
        permissions = permissions.Distinct().ToList();

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
            scopeDtos,
            user.IsSuspended ? "suspended" : (user.IsActive ? "active" : "inactive"),
            effectiveRoles,
            permissions,
            new List<string>(), // Auth0Roles placeholder
            user.IsPlatformAdmin);
    }
}
