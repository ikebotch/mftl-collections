using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;

using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Users.Commands.InviteUser;

[HasPermission("users.invite")]
public record InviteUserCommand(
    string Email,
    string Name,
    string Role,
    string ScopeType,
    Guid? TargetId,
    Guid? TenantId) : IRequest<Guid>, IHasScope
{
    public Guid? GetScopeId() => TargetId ?? TenantId;
}

public class InviteUserCommandHandler(
    IApplicationDbContext dbContext, 
    IAuth0Service auth0Service,
    ICurrentUserService currentUserService) : IRequestHandler<InviteUserCommand, Guid>
{
    public async Task<Guid> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ScopeType>(request.ScopeType, true, out var scopeType))
        {
            throw new ArgumentException($"Invalid scope type: {request.ScopeType}. Valid types are Organisation, Branch, Event, RecipientFund.");
        }

        var targetId = request.TargetId ?? request.TenantId;
        if (targetId == null && scopeType != ScopeType.Platform)
        {
             throw new ArgumentException("Target ID or Tenant ID is required for non-platform invitations.");
        }

        if (!currentUserService.IsPlatformAdmin)
        {
            if (scopeType == ScopeType.Platform)
            {
                throw new UnauthorizedAccessException("Only Platform Administrators can invite users with system-wide access.");
            }

            if (string.Equals(request.Role, "Platform Admin", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Only Platform Administrators can assign the Platform Admin role.");
            }
        }

        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), cancellationToken);

        if (existingUser != null)
        {
            throw new InvalidOperationException("User with this email already exists.");
        }

        // Proactively try to create in Auth0 if configured
        var auth0Id = await auth0Service.CreateUserAsync(request.Email, request.Name, request.Role, cancellationToken);

        var user = new User
        {
            Auth0Id = auth0Id ?? string.Empty,
            Email = request.Email,
            Name = request.Name,
            InviteStatus = UserInviteStatus.Pending,
            IsActive = false
        };

        dbContext.Users.Add(user);

        var assignment = new UserScopeAssignment
        {
            User = user,
            Role = request.Role,
            ScopeType = scopeType,
            TargetId = targetId
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

        // Add Domain Event for Outbox
        user.AddDomainEvent(new Domain.Events.UserInvitedEvent(
            user.Id,
            request.TenantId ?? Guid.Empty,
            user.Email,
            user.Name,
            request.Role));

        await dbContext.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
