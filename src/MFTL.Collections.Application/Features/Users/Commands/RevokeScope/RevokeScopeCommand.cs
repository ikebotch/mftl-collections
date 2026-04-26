using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Users.Commands.RevokeScope;

public record RevokeScopeCommand(Guid AssignmentId) : IRequest<bool>;

public class RevokeScopeCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<RevokeScopeCommand, bool>
{
    public async Task<bool> Handle(RevokeScopeCommand request, CancellationToken cancellationToken)
    {
        var assignment = await dbContext.UserScopeAssignments
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == request.AssignmentId, cancellationToken);
            
        if (assignment == null) return false;

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
