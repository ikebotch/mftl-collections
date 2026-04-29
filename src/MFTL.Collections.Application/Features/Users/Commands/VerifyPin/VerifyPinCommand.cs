using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Users.Commands.VerifyPin;

[HasPermission("self.view")]
public record VerifyPinCommand(string Pin, string? ExplicitUserId = null) : IRequest<bool>, IHasScope
{
    public Guid? GetScopeId() => null;
}

public class VerifyPinCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<VerifyPinCommand, bool>
{
    public async Task<bool> Handle(VerifyPinCommand request, CancellationToken cancellationToken)
    {
        var userId = request.ExplicitUserId ?? currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Auth0Id == userId, cancellationToken);

        if (user == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(user.Pin))
        {
            // If no PIN is set, the user must set one first.
            // We do not allow a default hardcoded PIN.
            return false;
        }

        return user.Pin == request.Pin;
    }
}
