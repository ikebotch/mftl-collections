using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Users.Commands.InviteUser;

public record InviteUserCommand(
    string Email,
    string Name,
    string Role,
    string ScopeType,
    Guid? TargetId,
    Guid? TenantId) : IRequest<Guid>;

public class InviteUserCommandHandler(IApplicationDbContext dbContext, IEmailService emailService) : IRequestHandler<InviteUserCommand, Guid>
{
    public async Task<Guid> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), cancellationToken);

        if (existingUser != null)
        {
            throw new InvalidOperationException("User with this email already exists.");
        }

        var user = new User
        {
            Email = request.Email,
            Name = request.Name,
            InviteStatus = UserInviteStatus.Pending,
            IsActive = false
        };

        dbContext.Users.Add(user);

        var scopeType = Enum.Parse<ScopeType>(request.ScopeType);
        AppRoles.Guard(request.Role);
        
        var assignment = new UserScopeAssignment
        {
            User = user,
            Role = request.Role,
            ScopeType = scopeType,
            TargetId = request.TargetId
        };

        dbContext.UserScopeAssignments.Add(assignment);

        // Audit Log
        var audit = new AuditLog
        {
            Action = "UserInvited",
            EntityName = "User",
            EntityId = user.Id.ToString(),
            Details = $"Invited {user.Email} with role {request.Role} on scope {request.ScopeType}",
            PerformedBy = "System Admin", // Ideally from CurrentUserContext
            TenantId = request.TenantId
        };
        dbContext.AuditLogs.Add(audit);

        await dbContext.SaveChangesAsync(cancellationToken);

        await emailService.SendInvitationAsync(request.Email, request.Name, request.Role);

        return user.Id;
    }
}
