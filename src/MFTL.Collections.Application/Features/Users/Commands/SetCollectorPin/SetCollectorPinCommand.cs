using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Users.Commands.SetCollectorPin;

public record SetCollectorPinCommand(string Pin) : IRequest<bool>;

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

        if (request.Pin.Length != 4 || !request.Pin.All(char.IsDigit))
        {
            throw new InvalidOperationException("PIN must be exactly 4 digits.");
        }

        user.Pin = request.Pin;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
