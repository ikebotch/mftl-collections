using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Webhooks.Auth0.Commands.UserCreated;

public record UserCreatedWebhookCommand(
    string Auth0Id,
    string Email,
    string Name,
    bool IsPlatformAdmin = false) : IRequest;

public class UserCreatedWebhookCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<UserCreatedWebhookCommand>
{
    public async Task Handle(UserCreatedWebhookCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Auth0Id == request.Auth0Id, cancellationToken);

        if (user == null)
        {
            // Try match by email
            user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), cancellationToken);

            if (user != null)
            {
                user.Auth0Id = request.Auth0Id;
                user.InviteStatus = UserInviteStatus.Accepted;
                user.IsActive = true;
                user.ModifiedAt = DateTimeOffset.UtcNow;
                user.ModifiedBy = "Webhook/Auth0/Link";
            }
            else
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Auth0Id = request.Auth0Id,
                    Email = request.Email,
                    Name = request.Name,
                    IsPlatformAdmin = request.IsPlatformAdmin,
                    IsActive = true,
                    InviteStatus = UserInviteStatus.Accepted,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "Webhook/Auth0/Provision"
                };
                dbContext.Users.Add(user);
            }
        }
        else
        {
            // Idempotent update
            bool changed = false;
            if (user.Name != request.Name && !string.IsNullOrEmpty(request.Name))
            {
                user.Name = request.Name;
                changed = true;
            }
            if (user.IsPlatformAdmin != request.IsPlatformAdmin)
            {
                user.IsPlatformAdmin = request.IsPlatformAdmin;
                changed = true;
            }

            if (changed)
            {
                user.ModifiedAt = DateTimeOffset.UtcNow;
                user.ModifiedBy = "Webhook/Auth0/Update";
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
