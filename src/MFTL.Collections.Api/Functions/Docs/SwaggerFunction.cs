using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Swashbuckle.AspNetCore.Swagger;
using Microsoft.Extensions.Hosting;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Api.Functions.Docs;

public class SwaggerFunction(
    ISwaggerProvider swaggerProvider, 
    IHostEnvironment environment,
    IScopeAccessService scopeService)
{
    [Function("SwaggerJson")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger/v1/swagger.json")] HttpRequest req)
    {
        // Publicly available in Dev. In Production, requires Platform Admin access.
        if (!environment.IsDevelopment())
        {
#pragma warning disable CS0618
            var hasAccess = await scopeService.HasPermissionAsync(Permissions.Platform.Manage);
#pragma warning restore CS0618
            if (!hasAccess) return new ForbidResult();
        }

        var swagger = swaggerProvider.GetSwagger("v1");
        
        return new OkObjectResult(swagger);
    }
}
