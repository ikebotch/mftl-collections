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

public class AssignScopeCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<AssignScopeCommand, bool>
{
    public async Task<bool> Handle(AssignScopeCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FindAsync(new object[] { request.UserId }, cancellationToken);
        if (user == null) throw new KeyNotFoundException("User not found.");

        var scopeType = Enum.Parse<ScopeType>(request.ScopeType);

        foreach (var role in request.Roles)
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
            Details = $"Assigned roles [{string.Join(", ", request.Roles)}] on scope {request.ScopeType} (Target: {request.TargetId?.ToString() ?? "Global"})",
            PerformedBy = "System Administrator"
        };
        dbContext.AuditLogs.Add(audit);

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
