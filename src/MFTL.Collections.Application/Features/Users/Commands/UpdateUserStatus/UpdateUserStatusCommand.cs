using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;

using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Users.Commands.UpdateUserStatus;

[HasPermission("users.update")]
public record UpdateUserStatusCommand(
    Guid Id,
    UserStatusAction Action) : IRequest<bool>, IHasScope
{
    public Guid? GetScopeId() => null;
}

public enum UserStatusAction
{
    Activate,
    Deactivate,
    Suspend,
    Unsuspend,
    CancelInvite,
    ResendInvite
}

public class UpdateUserStatusHandler(
    IApplicationDbContext dbContext,
    IAccessPolicyResolver policyResolver,
    ICurrentUserService currentUserService) : IRequestHandler<UpdateUserStatusCommand, bool>
{
    public async Task<bool> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        var policy = await policyResolver.ResolvePolicyAsync();
        var user = await policy.FilterUsers(dbContext.Users.AsQueryable())
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found or you do not have permission to manage this user.");
        }

        string detailAction = "";

        switch (request.Action)
        {
            case UserStatusAction.Activate:
                user.IsActive = true;
                user.IsSuspended = false;
                detailAction = "UserActivated";
                break;
            case UserStatusAction.Deactivate:
                user.IsActive = false;
                detailAction = "UserDeactivated";
                break;
            case UserStatusAction.Suspend:
                user.IsSuspended = true;
                detailAction = "UserSuspended";
                break;
            case UserStatusAction.Unsuspend:
                user.IsSuspended = false;
                detailAction = "UserReactivated";
                break;
            case UserStatusAction.CancelInvite:
                user.InviteStatus = UserInviteStatus.Cancelled;
                detailAction = "InviteCancelled";
                break;
            case UserStatusAction.ResendInvite:
                // Logic for resending email would go here
                detailAction = "InviteResent";
                break;
        }

        var audit = new AuditLog
        {
            Action = detailAction,
            EntityName = "User",
            EntityId = user.Id.ToString(),
            Details = $"Action {request.Action} performed on user {user.Email}",
            PerformedBy = "System Admin"
        };
        dbContext.AuditLogs.Add(audit);

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
