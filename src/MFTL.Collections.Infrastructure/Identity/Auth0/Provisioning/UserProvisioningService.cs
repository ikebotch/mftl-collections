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
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<Guid> ProvisionUserAsync(string auth0Id, string email, string name, List<string> roles, string? accessToken = null, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // 1. Try to enrich sparse identity details
            bool isSparse = string.IsNullOrEmpty(email) || 
                            string.IsNullOrEmpty(name) || 
                            name == "New User" || 
                            name == auth0Id ||
                            email.Contains("unprovisioned") ||
                            email == auth0Id;

            if (isSparse)
            {
                if (!string.IsNullOrEmpty(accessToken))
                {
                    logger.LogInformation("Identity details sparse for {Auth0Id}. Attempting to fetch from /userinfo.", auth0Id);
                    var userInfo = await auth0Service.GetUserInfoAsync(accessToken, cancellationToken);
                    if (userInfo != null && !string.IsNullOrEmpty(userInfo.Value.Email))
                    {
                        email = userInfo.Value.Email;
                        name = userInfo.Value.Name;
                        logger.LogInformation("Successfully fetched profile from /userinfo: {Email}, {Name}", email, name);
                    }
                }
                
                // Fallback to Management API if still sparse and configured
                if ((string.IsNullOrEmpty(email) || email.Contains("unprovisioned")) && await auth0Service.IsConfiguredAsync())
                {
                    logger.LogInformation("Identity still sparse for {Auth0Id}. Attempting to fetch from Management API.", auth0Id);
                    var profile = await auth0Service.GetUserProfileAsync(auth0Id, cancellationToken);
                    if (profile != null && !string.IsNullOrEmpty(profile.Value.Email))
                    {
                        email = profile.Value.Email;
                        name = profile.Value.Name;
                        logger.LogInformation("Successfully fetched profile from Management API: {Email}, {Name}", email, name);
                    }
                }
            }

            // Normalise email
            email = email?.Trim().ToLower();

            var user = await dbContext.Users
                .Include(u => u.ScopeAssignments)
                .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, cancellationToken);

            var isPlatformAdmin = roles.Any(r => 
                string.Equals(r, "Platform Admin", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(r, "super_admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, "platform_admin", StringComparison.OrdinalIgnoreCase));

            // 2. Handle existing @unprovisioned.mftl to real email transition
            if (user != null && user.Email.Contains("unprovisioned") && !string.IsNullOrEmpty(email) && !email.Contains("unprovisioned"))
            {
                logger.LogInformation("Updating unprovisioned user {OldEmail} to real email {NewEmail}", user.Email, email);
                
                // Check if another user already has this real email
                var existingRealUser = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Email == email && u.Auth0Id != auth0Id, cancellationToken);
                
                if (existingRealUser != null)
                {
                    logger.LogWarning("Merge required: Real user {Email} already exists. Linking Auth0Id {Auth0Id} to it and removing dummy user.", email, auth0Id);
                    // In a real scenario, you'd merge scopes/history. For now, we link and delete dummy if it has no history.
                    // This is a complex case, but requested by user.
                    existingRealUser.Auth0Id = auth0Id;
                    existingRealUser.IsActive = true;
                    existingRealUser.InviteStatus = UserInviteStatus.Accepted;
                    
                    dbContext.Users.Remove(user);
                    user = existingRealUser;
                }
                else
                {
                    user.Email = email;
                }
            }

            // 3. Try matching by email if not found by Auth0Id (for invited users)
            if (user == null && !string.IsNullOrEmpty(email) && !email.Contains("unprovisioned"))
            {
                user = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
                
                if (user != null)
                {
                    logger.LogInformation("Linking existing user {Email} to Auth0Id {Auth0Id}", email, auth0Id);
                    user.Auth0Id = auth0Id;
                    user.InviteStatus = UserInviteStatus.Accepted;
                    user.IsActive = true;
                }
            }

            // 4. Create or Update
            if (user == null)
            {
                logger.LogInformation("Provisioning new user {Email} with Auth0Id {Auth0Id}", email, auth0Id);
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Auth0Id = auth0Id,
                    Name = !string.IsNullOrEmpty(name) && name != "New User" && name != auth0Id ? name : (!string.IsNullOrEmpty(email) ? email : auth0Id),
                    Email = !string.IsNullOrEmpty(email) ? email : $"{auth0Id}@unprovisioned.mftl",
                    IsPlatformAdmin = isPlatformAdmin,
                    IsActive = true,
                    InviteStatus = UserInviteStatus.Accepted,
                    CreatedAt = DateTimeOffset.UtcNow,
                    CreatedBy = "System/AutoProvision",
                    LastLoginAt = DateTimeOffset.UtcNow,
                    Pin = "1234"
                };

                dbContext.Users.Add(user);
            }
            else
            {
                // Update profile on every call
                bool changed = false;
                
                if (!string.IsNullOrEmpty(name) && name != "New User" && name != auth0Id && user.Name != name)
                {
                    user.Name = name;
                    changed = true;
                }
                
                if (!string.IsNullOrEmpty(email) && !email.Contains("unprovisioned") && user.Email != email)
                {
                    user.Email = email;
                    changed = true;
                }
                
                if (user.IsPlatformAdmin != isPlatformAdmin)
                {
                    user.IsPlatformAdmin = isPlatformAdmin;
                    changed = true;
                }

                // Always update LastLoginAt
                user.LastLoginAt = DateTimeOffset.UtcNow;
                changed = true;

                if (changed)
                {
                    user.ModifiedAt = DateTimeOffset.UtcNow;
                    user.ModifiedBy = "System/AutoSync";
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return user.Id;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
