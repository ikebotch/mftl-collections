using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
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
        "GetPublicEventBySlug",
        "ListPublicRecipientFunds",
        "InitiatePublicContribution",
        "GetPublicPaymentStatus",
        "GetPublicReceipt",
        "GetPublicReceiptByContribution",
    };

    public static bool RequiresTenant(string functionName) => !PlatformFunctionNames.Contains(functionName);

    public static TenantRequestResolution Evaluate(
        string functionName,
        IEnumerable<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> headers,
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

        var tenantHeader = headers.FirstOrDefault(h => h.Key.Equals(options.HeaderName, StringComparison.OrdinalIgnoreCase));
        if (!Microsoft.Extensions.Primitives.StringValues.IsNullOrEmpty(tenantHeader.Value))
        {
            var tenantHeaderValue = tenantHeader.Value.FirstOrDefault() ?? string.Empty;
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

        if (!string.IsNullOrEmpty(options.DefaultTenantId) && Guid.TryParse(options.DefaultTenantId, out var defaultId))
        {
            return new TenantRequestResolution(true, true, defaultId, "default");
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
