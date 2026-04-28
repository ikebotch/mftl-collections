using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using System.Security.Claims;

namespace MFTL.Collections.Api.Middleware;

public sealed class UserProvisioningMiddleware : IFunctionsWorkerMiddleware
{
    private const string RoleClaim = "https://mftl.com/roles";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (!context.IsHttpTrigger())
        {
            await next(context);
            return;
        }

        var userService = context.InstanceServices.GetRequiredService<ICurrentUserService>();
        if (!userService.IsAuthenticated || string.IsNullOrEmpty(userService.UserId))
        {
            await next(context);
            return;
        }

        var dbContext = context.InstanceServices.GetRequiredService<IApplicationDbContext>();
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Auth0Id == userService.UserId);

        var email = userService.Email ?? userService.User?.FindFirstValue(ClaimTypes.Email) ?? "";

        // If not found by Auth0Id, try matching by email (for invited users)
        if (user == null && !string.IsNullOrEmpty(email))
        {
            user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
            
            if (user != null)
            {
                // Link the Auth0Id to the existing record
                user.Auth0Id = userService.UserId;
                user.InviteStatus = UserInviteStatus.Accepted;
                user.IsActive = true;
                user.ModifiedAt = DateTimeOffset.UtcNow;
                user.ModifiedBy = "System/AutoLink";
            }
        }

        var roles = userService.User?.Claims
            .Where(c => c.Type == RoleClaim || c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .Distinct()
            .ToList() ?? new List<string>();
            
        var isPlatformAdmin = roles.Any(r => 
            string.Equals(r, "Platform Admin", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(r, "super_admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "platform_admin", StringComparison.OrdinalIgnoreCase));
        var name = userService.User?.FindFirstValue("name") ?? userService.User?.FindFirstValue(ClaimTypes.Name) ?? "New User";

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Auth0Id = userService.UserId,
                Name = name,
                Email = email,
                IsPlatformAdmin = isPlatformAdmin,
                IsActive = true,
                InviteStatus = UserInviteStatus.Accepted,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "System/AutoProvision"
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(CancellationToken.None);
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
            if (user.IsPlatformAdmin != isPlatformAdmin)
            {
                user.IsPlatformAdmin = isPlatformAdmin;
                changed = true;
            }

            if (changed)
            {
                user.ModifiedAt = DateTimeOffset.UtcNow;
                user.ModifiedBy = "System/AutoSync";
                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
        }

        await next(context);
    }
}
