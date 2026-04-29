using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;

using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Users.Commands.RevokeScope;

[HasPermission("users.update")]
public record RevokeScopeCommand(Guid AssignmentId) : IRequest<bool>, IHasScope
{
    public Guid? GetScopeId() => null; // Scope of the assignment will be checked in handler
}

public class RevokeScopeCommandHandler(
    IApplicationDbContext dbContext,
    IPermissionEvaluator permissionEvaluator,
    ICurrentUserService currentUserService) : IRequestHandler<RevokeScopeCommand, bool>
{
    public async Task<bool> Handle(RevokeScopeCommand request, CancellationToken cancellationToken)
    {
        var assignment = await dbContext.UserScopeAssignments
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken);
            
        if (assignment == null) return false;

        // Security: Prevent self-revocation (to avoid lockout)
        if (assignment.User.Auth0Id == currentUserService.UserId && !currentUserService.IsPlatformAdmin)
        {
            throw new UnauthorizedAccessException("You cannot revoke your own administrative assignments.");
        }

        // Security: Check permission for the specific scope
        if (!await permissionEvaluator.HasPermissionAsync("users.assign_role", assignment.TargetId))
        {
            throw new UnauthorizedAccessException("You do not have permission to revoke roles in this scope.");
        }

        dbContext.UserScopeAssignments.Remove(assignment);

        // Audit Log
        var audit = new AuditLog
        {
            Action = "ScopeRevoked",
            EntityName = "User",
            EntityId = assignment.User.Id.ToString(),
            Details = $"Revoked role {assignment.Role} on scope {assignment.ScopeType}",
            PerformedBy = "System Admin"
        };
        dbContext.AuditLogs.Add(audit);

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
