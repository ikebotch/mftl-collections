using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Api.Middleware;

public class AuthenticationMiddleware(IConfiguration configuration) : IFunctionsWorkerMiddleware
{
    private readonly bool _bypassAuth = configuration.GetValue<bool>("Values:DEV_AUTH_BYPASS") || configuration.GetValue<bool>("DEV_AUTH_BYPASS");

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var logger = context.GetLogger<AuthenticationMiddleware>();
        logger.LogWarning("AuthenticationMiddleware: INVOKED for {FunctionName}", context.FunctionDefinition.Name);

        if (!context.IsHttpTrigger())
        {
            await next(context);
            return;
        }

        if (_bypassAuth)
        {
            await next(context);
            return;
        }

        var httpContext = context.GetHttpContext();

        if (httpContext == null)
        {
            var httpContextAccessor = context.InstanceServices.GetService<IHttpContextAccessor>();
            httpContext = httpContextAccessor?.HttpContext;
        }

        if (httpContext == null)
        {
            logger.LogWarning("AuthenticationMiddleware: HttpContext is NULL. Function: {FunctionName}, InvocationId: {InvocationId}", 
                context.FunctionDefinition.Name, context.InvocationId);
        }
        else
        {
            logger.LogWarning("AuthenticationMiddleware: Processing request {Method} {Path}", 
                httpContext.Request.Method, httpContext.Request.Path);
            
            foreach (var header in httpContext.Request.Headers)
            {
                if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    var val = header.Value.ToString();
                    logger.LogWarning("AuthenticationMiddleware: Found Authorization header. Length: {Length}. Starts with: {Start}", 
                        val.Length, val.Length > 10 ? val.Substring(0, 10) : "too short");
                }
            }

            if (!httpContext.Request.Headers.ContainsKey("Authorization"))
            {
                logger.LogWarning("AuthenticationMiddleware: No Authorization header found in HttpContext. Request Headers count: {Count}", 
                    httpContext.Request.Headers.Count);
            }

            try
            {
                var result = await httpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
                if (result.Succeeded && result.Principal != null)
                {
                    httpContext.User = result.Principal;
                    
                    // Explicitly set the user and token in CurrentUserService if possible
                    var currentUserService = context.InstanceServices.GetService<ICurrentUserService>();
                    if (currentUserService is MFTL.Collections.Infrastructure.Identity.CurrentUserService cus)
                    {
                        cus.SetUser(result.Principal);
                        
                        var authHeader = httpContext.Request.Headers["Authorization"].ToString();
                        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            var token = authHeader.Substring("Bearer ".Length).Trim();
                            cus.SetToken(token);
                        }
                    }
                    
                    logger.LogWarning("AuthenticationMiddleware: Authentication succeeded for user {UserId}.", 
                        result.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
                }
                else
                {
                    logger.LogWarning("AuthenticationMiddleware: Authentication failed or returned no principal. Succeeded={Succeeded}, Failure={Failure}", 
                        result.Succeeded, result.Failure?.Message);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AuthenticationMiddleware: Exception during AuthenticateAsync.");
            }
        }

        await next(context);
    }
}
