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

public class UserCreatedWebhookCommandHandler(IUserProvisioningService provisioningService) : IRequestHandler<UserCreatedWebhookCommand>
{
    public async Task Handle(UserCreatedWebhookCommand request, CancellationToken cancellationToken)
    {
        var roles = request.IsPlatformAdmin ? new List<string> { "Platform Admin" } : new List<string>();
        
        await provisioningService.ProvisionUserAsync(
            request.Auth0Id, 
            request.Email, 
            request.Name, 
            roles, 
            null,
            null,
            null,
            null,
            cancellationToken);
    }
}
