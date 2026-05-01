using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Common.Security;

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
                RoleNameNormalizer.Normalize(a.Role),
                a.ScopeType.ToString(),
                a.TargetId,
                targetName));
        }

        // Determine effective roles scoped to the active tenant context.
        var effectiveRoleKeys = new List<string>();
        var permissions = new List<string>();
        Guid? activeTenantId = tenantContext.TenantId;

        if (user.IsPlatformAdmin)
        {
            effectiveRoleKeys.Add(AppRoles.PlatformAdmin);
            permissions.Add("*");
        }
        else
        {
            var availableTenants = new HashSet<Guid>();

            // Resolve unique tenants from all assignments
            foreach (var a in user.ScopeAssignments.Where(x => x.TargetId.HasValue))
            {
                if (a.ScopeType == Domain.Entities.ScopeType.Tenant)
                {
                    availableTenants.Add(a.TargetId!.Value);
                }
                else if (a.ScopeType == Domain.Entities.ScopeType.Branch)
                {
                    var tId = await dbContext.Branches.IgnoreQueryFilters()
                        .Where(b => b.Id == a.TargetId)
                        .Select(b => b.TenantId)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (tId != Guid.Empty) availableTenants.Add(tId);
                }
                else if (a.ScopeType == Domain.Entities.ScopeType.Event)
                {
                    var tId = await dbContext.Events.IgnoreQueryFilters()
                        .Where(e => e.Id == a.TargetId)
                        .Select(e => e.TenantId)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (tId != Guid.Empty) availableTenants.Add(tId);
                }
                else if (a.ScopeType == Domain.Entities.ScopeType.RecipientFund)
                {
                    var tId = await dbContext.RecipientFunds.IgnoreQueryFilters()
                        .Where(f => f.Id == a.TargetId)
                        .Select(f => f.Event.TenantId)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (tId != Guid.Empty) availableTenants.Add(tId);
                }
            }

            // Bootstrap Logic: If no tenant is active but user has assignments for exactly one unique tenant, use it.
            if (activeTenantId == null && availableTenants.Count == 1)
            {
                activeTenantId = availableTenants.First();
            }

            if (activeTenantId.HasValue && activeTenantId.Value != Guid.Empty)
            {
                // Scoped: only include roles from assignments matching the active tenant
                var candidateRoles = user.ScopeAssignments
                    .Where(a => 
                        (a.ScopeType == Domain.Entities.ScopeType.Tenant && a.TargetId == activeTenantId.Value) ||
                        (a.TargetId.HasValue)) // Child assignments need async check below
                    .ToList();

                foreach (var a in candidateRoles)
                {
                    bool isMatch = false;
                    if (a.ScopeType == Domain.Entities.ScopeType.Tenant)
                    {
                        isMatch = a.TargetId == activeTenantId.Value;
                    }
                    else if (a.ScopeType == Domain.Entities.ScopeType.Branch)
                    {
                        isMatch = await dbContext.Branches.IgnoreQueryFilters()
                            .AnyAsync(b => b.Id == a.TargetId && b.TenantId == activeTenantId.Value, cancellationToken);
                    }
                    else if (a.ScopeType == Domain.Entities.ScopeType.Event)
                    {
                        isMatch = await dbContext.Events.IgnoreQueryFilters()
                            .AnyAsync(e => e.Id == a.TargetId && e.TenantId == activeTenantId.Value, cancellationToken);
                    }
                    else if (a.ScopeType == Domain.Entities.ScopeType.RecipientFund)
                    {
                        isMatch = await dbContext.RecipientFunds.IgnoreQueryFilters()
                            .AnyAsync(f => f.Id == a.TargetId && f.Event.TenantId == activeTenantId.Value, cancellationToken);
                    }

                    if (isMatch)
                    {
                        effectiveRoleKeys.Add(RoleNameNormalizer.Normalize(a.Role));
                    }
                }

                effectiveRoleKeys = effectiveRoleKeys.Distinct().ToList();

                // Fetch permissions for the effective roles
                if (effectiveRoleKeys.Count > 0)
                {
                    var rolePermissions = await dbContext.RolePermissions
                        .IgnoreQueryFilters()
                        .Where(rp => effectiveRoleKeys.Contains(rp.RoleName))
                        .Select(rp => rp.PermissionKey)
                        .ToListAsync(cancellationToken);

                    permissions.AddRange(rolePermissions);
                }
            }
        }

        var effectiveRoles = effectiveRoleKeys.Select(AppRoles.GetDisplayName).ToList();
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
            effectiveRoleKeys,
            effectiveRoles,
            permissions,
            new List<string>(), // Auth0Roles placeholder
            user.IsPlatformAdmin,
            activeTenantId);
    }
}
