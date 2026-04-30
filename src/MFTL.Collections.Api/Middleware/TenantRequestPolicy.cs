using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.AspNetCore.Http;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Infrastructure.Configuration;

namespace MFTL.Collections.Api.Middleware;

public sealed record TenantRequestResolution(
    bool RequiresTenant,
    bool Success,
    Guid? TenantId,
    string? Identifier,
    HttpStatusCode? StatusCode = null,
    string? Message = null,
    IReadOnlyList<string>? Errors = null);

public static class TenantRequestPolicy
{
    private static readonly HashSet<string> PlatformFunctionNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PaymentWebhook",
        "ScalarUi",
        "RenderOAuth2Redirect",
        "RenderOpenApiDocument",
        "RenderSwaggerDocument",
        "RenderSwaggerUI",
        "GetMe",
        "GetCollectorMe",
        "GetCollectorAssignments",
        "ListTenants",
    };

    public static bool RequiresTenant(string functionName) => !PlatformFunctionNames.Contains(functionName);

    public static TenantRequestResolution Evaluate(
        string functionName,
        IHeaderDictionary headers,
        TenantResolutionResult resolutionResult,
        TenantResolutionOptions options)
    {
        if (!RequiresTenant(functionName))
        {
            return new TenantRequestResolution(false, true, null, null);
        }

        if (resolutionResult.Success && resolutionResult.TenantId.HasValue)
        {
            return new TenantRequestResolution(
                true,
                true,
                resolutionResult.TenantId,
                resolutionResult.Identifier);
        }

        if (headers.TryGetValue(options.HeaderName, out var tenantHeaderValues))
        {
            var tenantHeaderValue = tenantHeaderValues.FirstOrDefault() ?? string.Empty;
            return new TenantRequestResolution(
                true,
                false,
                null,
                null,
                HttpStatusCode.BadRequest,
                "Invalid tenant header.",
                new[]
                {
                    $"The '{options.HeaderName}' header must be a valid tenant UUID. Received '{tenantHeaderValue}'.",
                });
        }

        return new TenantRequestResolution(
            true,
            false,
            null,
            null,
            HttpStatusCode.BadRequest,
            "Tenant header is required.",
            new[]
            {
                $"The '{options.HeaderName}' header is required for tenant-scoped routes.",
            });
    }

    public static async Task<HttpResponseData> CreateErrorResponseAsync(
        HttpRequestData request,
        TenantRequestResolution resolution)
    {
        var response = request.CreateResponse(resolution.StatusCode ?? HttpStatusCode.BadRequest);
        await response.WriteAsJsonAsync(new ApiResponse(
            false,
            resolution.Message,
            resolution.Errors,
            request.GetOrCreateCorrelationId()));
        return response;
    }
}
