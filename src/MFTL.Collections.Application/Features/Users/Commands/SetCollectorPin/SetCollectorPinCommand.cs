using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Users.Commands.SetCollectorPin;

[HasPermission("self.update")]
public record SetCollectorPinCommand(string Pin, string? OldPin = null) : IRequest<bool>, IHasScope
{
    public Guid? GetScopeId() => null;
}

public class SetCollectorPinCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<SetCollectorPinCommand, bool>
{
    public async Task<bool> Handle(SetCollectorPinCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Auth0Id == userId, cancellationToken);

        if (user == null)
        {
            throw new UnauthorizedAccessException("User profile not found.");
        }

        // Verify old PIN if it exists
        if (!string.IsNullOrEmpty(user.Pin))
        {
            if (string.IsNullOrEmpty(request.OldPin))
            {
                throw new InvalidOperationException("Current PIN is required to set a new one.");
            }

            if (user.Pin != request.OldPin)
            {
                throw new InvalidOperationException("Current PIN is incorrect.");
            }
        }

        if (request.Pin.Length != 4 || !request.Pin.All(char.IsDigit))
        {
            throw new InvalidOperationException("PIN must be exactly 4 digits.");
        }

        user.Pin = request.Pin;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
