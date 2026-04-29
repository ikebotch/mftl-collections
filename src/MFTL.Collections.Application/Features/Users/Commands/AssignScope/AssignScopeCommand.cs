using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Users.Commands.AssignScope;

public record AssignScopeCommand(
    Guid UserId,
    IEnumerable<string> Roles,
    string ScopeType,
    Guid? TargetId) : IRequest<bool>;

public class AssignScopeCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<AssignScopeCommand, bool>
{
    public async Task<bool> Handle(AssignScopeCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
        if (user == null) throw new KeyNotFoundException("User not found.");

        var roles = request.Roles ?? Enumerable.Empty<string>();
        
        string scopeTypeString = request.ScopeType;
        if (scopeTypeString == "Tenant") scopeTypeString = "Organisation";

        if (!Enum.TryParse<ScopeType>(scopeTypeString, true, out var scopeType))
        {
            throw new ArgumentException($"Invalid ScopeType: {request.ScopeType}");
        }

        // Security check: Only platform admins can assign platform scope or platform admin role
        if (!currentUserService.IsPlatformAdmin)
        {
            if (scopeType == ScopeType.Platform)
            {
                throw new UnauthorizedAccessException("Only Platform Administrators can assign system-wide access.");
            }

            if (roles.Any(r => string.Equals(r, "Platform Admin", StringComparison.OrdinalIgnoreCase)))
            {
                throw new UnauthorizedAccessException("Only Platform Administrators can assign the Platform Admin role.");
            }
        }

        foreach (var role in roles)
        {
            var assignment = new UserScopeAssignment
            {
                User = user,
                Role = role,
                ScopeType = scopeType,
                TargetId = request.TargetId
            };

            dbContext.UserScopeAssignments.Add(assignment);
        }

        // Audit Log
        var audit = new AuditLog
        {
            UserId = user.Id,
            Action = "ScopesAssigned",
            EntityName = "User",
            EntityId = user.Id.ToString(),
            Details = $"Assigned roles [{string.Join(", ", roles)}] on scope {request.ScopeType} (Target: {request.TargetId?.ToString() ?? "Global"})",
            PerformedBy = "System Administrator"
        };
        dbContext.AuditLogs.Add(audit);

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
