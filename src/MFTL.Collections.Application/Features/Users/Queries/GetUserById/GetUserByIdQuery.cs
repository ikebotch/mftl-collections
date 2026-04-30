using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Users.Queries.GetUserById;

public record GetUserByIdQuery(Guid Id) : IRequest<UserDetailDto>;

public class GetUserByIdQueryHandler(
    IApplicationDbContext dbContext,
    ITenantContext tenantContext) : IRequestHandler<GetUserByIdQuery, UserDetailDto>
{
    public async Task<UserDetailDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user == null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        // Always return ALL scope assignments so the frontend can derive available tenants
        var scopeDtos = new List<ScopeAssignmentDto>();
        foreach (var a in user.ScopeAssignments)
        {
            string? targetName = null;
            if (a.ScopeType == Domain.Entities.ScopeType.Event && a.TargetId.HasValue)
            {
                targetName = await dbContext.Events.IgnoreQueryFilters().Where(e => e.Id == a.TargetId).Select(e => e.Title).FirstOrDefaultAsync(cancellationToken);
            }
            else if (a.ScopeType == Domain.Entities.ScopeType.RecipientFund && a.TargetId.HasValue)
            {
                targetName = await dbContext.RecipientFunds.IgnoreQueryFilters().Where(f => f.Id == a.TargetId).Select(f => f.Name).FirstOrDefaultAsync(cancellationToken);
            }
            else if (a.ScopeType == Domain.Entities.ScopeType.Branch && a.TargetId.HasValue)
            {
                targetName = await dbContext.Branches.IgnoreQueryFilters().Where(b => b.Id == a.TargetId).Select(b => b.Name).FirstOrDefaultAsync(cancellationToken);
            }
            else if (a.ScopeType == Domain.Entities.ScopeType.Tenant && a.TargetId.HasValue)
            {
                targetName = await dbContext.Tenants.IgnoreQueryFilters().Where(t => t.Id == a.TargetId).Select(t => t.Name).FirstOrDefaultAsync(cancellationToken);
            }

            scopeDtos.Add(new ScopeAssignmentDto(
                a.Id,
                a.Role,
                a.ScopeType.ToString(),
                a.TargetId,
                targetName));
        }

        // Determine effective roles scoped to the active tenant context.
        // If no tenant is active (bootstrap), return empty permissions but all scopeAssignments.
        var effectiveRoles = new List<string>();
        var permissions = new List<string>();

        if (user.IsPlatformAdmin)
        {
            effectiveRoles.Add("Platform Admin");
            permissions.Add("*");
        }
        else
        {
            var activeTenantId = tenantContext.TenantId;

            if (activeTenantId.HasValue && activeTenantId.Value != Guid.Empty)
            {
                // Scoped: only include roles from assignments matching the active tenant
                // Tenant-scope assignment covers all child scopes within that tenant
                // Branch/Event/Fund assignments also qualify if they belong to the active tenant
                var tenantRoles = user.ScopeAssignments
                    .Where(a => a.ScopeType == Domain.Entities.ScopeType.Platform ||
                               (a.ScopeType == Domain.Entities.ScopeType.Tenant && a.TargetId == activeTenantId.Value) ||
                                a.ScopeType == Domain.Entities.ScopeType.Branch ||
                                a.ScopeType == Domain.Entities.ScopeType.Event ||
                                a.ScopeType == Domain.Entities.ScopeType.RecipientFund)
                    .Select(a => a.Role)
                    .Distinct()
                    .ToList();

                // For branch/event/fund scopes, we include them because the middleware already
                // validated that the user's branches belong to the active tenant.
                // The AllowedBranchIds in the middleware ensures branch isolation.
                effectiveRoles.AddRange(tenantRoles);

                // Fetch permissions for the tenant-scoped roles only
                var rolePermissions = await dbContext.RolePermissions
                    .IgnoreQueryFilters()
                    .Where(rp => effectiveRoles.Contains(rp.RoleName))
                    .Select(rp => rp.PermissionKey)
                    .ToListAsync(cancellationToken);

                permissions.AddRange(rolePermissions);
            }
            // else: no active tenant = bootstrap context.
            // Return empty permissions; frontend must select a tenant first.
            // scopeAssignments are still returned so frontend can build tenant switcher.
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
