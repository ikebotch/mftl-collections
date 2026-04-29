using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Api.Middleware;

public class EndpointAccessPolicyMiddleware(IConfiguration configuration, ILogger<EndpointAccessPolicyMiddleware> logger) : IFunctionsWorkerMiddleware
{
    private readonly bool _bypassAuth = configuration.GetValue<bool>("Values:DEV_AUTH_BYPASS") || configuration.GetValue<bool>("DEV_AUTH_BYPASS");

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (!context.IsHttpTrigger())
        {
            await next(context);
            return;
        }

        var functionName = context.FunctionDefinition.Name;
        
        if (!EndpointAccessPolicies.Registry.TryGetValue(functionName, out var policy))
        {
            logger.LogCritical("Endpoint Security Violation: Function '{FunctionName}' is not mapped to an access policy. Failing closed.", functionName);
            await SetResponseAsync(context, HttpStatusCode.Forbidden, "Endpoint security configuration error.");
            return;
        }

        if (_bypassAuth && policy.Type != EndpointAccessPolicyType.WebhookSecret)
        {
            await next(context);
            return;
        }

        var isAuthorized = policy.Type switch
        {
            EndpointAccessPolicyType.Public => true,
            EndpointAccessPolicyType.Authenticated => await IsAuthenticatedAsync(context),
            EndpointAccessPolicyType.Permission => await HasPermissionAsync(context, policy.RequiredPermission),
            EndpointAccessPolicyType.WebhookSecret => await ValidateWebhookSecretAsync(context, policy.SecretName),
            EndpointAccessPolicyType.InternalOnly => IsInternalRequest(context),
            EndpointAccessPolicyType.PlatformOnly => await IsPlatformAdminAsync(context),
            _ => false
        };

        if (!isAuthorized)
        {
            logger.LogWarning("Unauthorized access attempt to '{FunctionName}' with policy '{PolicyType}'.", functionName, policy.Type);
            var status = policy.Type == EndpointAccessPolicyType.Public || policy.Type == EndpointAccessPolicyType.Authenticated 
                ? HttpStatusCode.Unauthorized 
                : HttpStatusCode.Forbidden;
            
            await SetResponseAsync(context, status, "Unauthorized access.");
            return;
        }

        await next(context);
    }

    private async Task<bool> IsAuthenticatedAsync(FunctionContext context)
    {
        var currentUserService = context.InstanceServices.GetRequiredService<ICurrentUserService>();
        return currentUserService.IsAuthenticated;
    }

    private async Task<bool> HasPermissionAsync(FunctionContext context, string? permission)
    {
        if (string.IsNullOrEmpty(permission)) return false;

        var currentUserService = context.InstanceServices.GetRequiredService<ICurrentUserService>();
        if (!currentUserService.IsAuthenticated) return false;

        var permissionEvaluator = context.InstanceServices.GetRequiredService<IPermissionEvaluator>();
        return await permissionEvaluator.HasPermissionAsync(permission);
    }

    private async Task<bool> IsPlatformAdminAsync(FunctionContext context)
    {
        var currentUserService = context.InstanceServices.GetRequiredService<ICurrentUserService>();
        return currentUserService.IsAuthenticated && currentUserService.IsPlatformAdmin;
    }

    private async Task<bool> ValidateWebhookSecretAsync(FunctionContext context, string? secretName)
    {
        if (string.IsNullOrEmpty(secretName)) return false;

        var request = await context.GetHttpRequestDataAsync();
        if (request == null) return false;

        var secret = configuration[$"Values:{secretName}"] ?? configuration[secretName];
        if (string.IsNullOrEmpty(secret))
        {
            logger.LogError("Webhook secret '{SecretName}' is not configured.", secretName);
            return false;
        }

        if (request.Headers.TryGetValues("X-Webhook-Secret", out var values))
        {
            return values.Contains(secret);
        }

        return false;
    }

    private bool IsInternalRequest(FunctionContext context)
    {
        // For now, reject all unless we implement a specific internal key or IP whitelist
        return false;
    }

    private async Task SetResponseAsync(FunctionContext context, HttpStatusCode statusCode, string message)
    {
        var request = await context.GetHttpRequestDataAsync();
        if (request != null)
        {
            var response = request.CreateResponse(statusCode);
            await response.WriteStringAsync(message);
            context.GetInvocationResult().Value = response;
        }
    }
}
