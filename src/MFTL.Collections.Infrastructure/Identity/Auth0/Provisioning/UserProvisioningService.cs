using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Infrastructure.Identity.Auth0.Provisioning;

public class UserProvisioningService(
    IApplicationDbContext dbContext,
    IAuth0Service auth0Service,
    ILogger<UserProvisioningService> logger) : IUserProvisioningService
{
    public async Task<Guid> ProvisionUserAsync(string auth0Id, string email, string name, List<string> roles, CancellationToken cancellationToken = default)
    {
        // If email or name are missing (common in access tokens), try to fetch from Auth0 Management API
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(name) || name == "New User")
        {
            logger.LogInformation("Identity details sparse for {Auth0Id}. Attempting to fetch full profile from Auth0.", auth0Id);
            var profile = await auth0Service.GetUserProfileAsync(auth0Id, cancellationToken);
            if (profile != null)
            {
                email = profile.Value.Email;
                name = profile.Value.Name;
                logger.LogInformation("Successfully fetched profile from Auth0: {Email}, {Name}", email, name);
            }
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, cancellationToken);

        var isPlatformAdmin = roles.Any(r => 
            string.Equals(r, "Platform Admin", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(r, "super_admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "platform_admin", StringComparison.OrdinalIgnoreCase));

        // If not found by Auth0Id, try matching by email (for invited users)
        if (user == null && !string.IsNullOrEmpty(email))
        {
            user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
            
            if (user != null)
            {
                logger.LogInformation("Linking existing user {Email} to Auth0Id {Auth0Id}", email, auth0Id);
                user.Auth0Id = auth0Id;
                user.InviteStatus = UserInviteStatus.Accepted;
                user.IsActive = true;
                user.ModifiedAt = DateTimeOffset.UtcNow;
                user.ModifiedBy = "System/AutoLink";
            }
        }

        if (user == null)
        {
            logger.LogInformation("Provisioning new user {Email} with Auth0Id {Auth0Id}", email, auth0Id);
            user = new User
            {
                Id = Guid.NewGuid(),
                Auth0Id = auth0Id,
                Name = !string.IsNullOrEmpty(name) && name != "New User" ? name : (!string.IsNullOrEmpty(email) ? email : auth0Id),
                Email = !string.IsNullOrEmpty(email) ? email : $"{auth0Id}@unprovisioned.local",
                IsPlatformAdmin = isPlatformAdmin,
                IsActive = true,
                InviteStatus = UserInviteStatus.Accepted,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "System/AutoProvision"
            };

            dbContext.Users.Add(user);
        }
        else
        {
            // Update name or admin status if it changed in Auth0
            bool changed = false;
            if (user.Name != name && !string.IsNullOrEmpty(name) && name != "New User")
            {
                user.Name = name;
                changed = true;
            }
            if (!string.IsNullOrEmpty(email) && user.Email != email)
            {
                user.Email = email;
                changed = true;
            }
            if (user.IsPlatformAdmin != isPlatformAdmin)
            {
                user.IsPlatformAdmin = isPlatformAdmin;
                changed = true;
            }

            if (changed)
            {
                user.ModifiedAt = DateTimeOffset.UtcNow;
                user.ModifiedBy = "System/AutoSync";
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return user.Id;
    }
}
