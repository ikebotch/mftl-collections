using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Infrastructure.Identity.Auth0.Provisioning;
using MFTL.Collections.Contracts.Common;

namespace MFTL.Collections.Api.Functions.Admin;

public class ProvisioningFunctions(
    Auth0ProvisioningService provisioningService,
    ILogger<ProvisioningFunctions> logger)
{
    [Function("Auth0Provisioning")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/auth0/provision")] HttpRequest req)
    {
        logger.LogInformation("C# HTTP trigger function processed a request for Auth0 Provisioning.");

        var applyStr = req.Query["apply"];
        bool apply = bool.TryParse(applyStr, out var b) && b;

        try
        {
            await provisioningService.ProvisionAsync(apply);
            return new OkObjectResult(new ApiResponse<string>(true, apply ? "Auth0 provisioning completed successfully." : "Auth0 provisioning dry-run completed. No changes applied."));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Auth0 provisioning.");
            return new BadRequestObjectResult(new ApiResponse(false, $"Provisioning failed: {ex.Message}"));
        }
    }
}
