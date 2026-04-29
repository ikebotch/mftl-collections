using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

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
        var provisioningService = context.InstanceServices.GetRequiredService<IUserProvisioningService>();
        var logger = context.GetLogger<UserProvisioningMiddleware>();
        
        if (!userService.IsAuthenticated || string.IsNullOrEmpty(userService.UserId))
        {
            await next(context);
            return;
        }

        var email = userService.Email ?? "";
        var roles = userService.Roles.ToList();
        var name = userService.Name ?? "New User";
        var nickname = userService.Nickname;
        var picture = userService.Picture;

        try
        {
            await provisioningService.ProvisionUserAsync(
                userService.UserId, 
                email, 
                name, 
                roles, 
                userService.AccessToken, 
                nickname,
                picture,
                userService.PhoneNumber,
                context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to provision user {UserId} in middleware", userService.UserId);
        }

        await next(context);
    }
}
