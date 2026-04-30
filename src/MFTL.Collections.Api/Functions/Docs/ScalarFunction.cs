using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using MFTL.Collections.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Api.Functions.Docs;

public class ScalarFunction(
    IOptions<ScalarOptions> options, 
    IHostEnvironment environment,
    IScopeAccessService scopeService)
{
    [Function("ScalarUi")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "docs/scalar")] HttpRequest req)
    {
        // Publicly available in Dev. In Production, requires Platform Admin access.
        if (!environment.IsDevelopment())
        {
#pragma warning disable CS0618
            var hasAccess = await scopeService.HasPermissionAsync(Permissions.Platform.Manage);
#pragma warning restore CS0618
            if (!hasAccess) return new ForbidResult();
        }

        // Point to the Swagger JSON endpoint served by SwaggerFunction
        var swaggerUrl = "/api/swagger/v1/swagger.json";
        
        var html = $@"
<!doctype html>
<html>
  <head>
    <title>{options.Value.Title}</title>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <style>
      body {{ margin: 0; }}
    </style>
  </head>
  <body>
    <script
      id=""api-reference""
      data-url=""{swaggerUrl}""></script>
    <script src=""https://cdn.jsdelivr.net/npm/@scalar/api-reference""></script>
  </body>
</html>";

        return new ContentResult
        {
            Content = html,
            ContentType = "text/html",
            StatusCode = 200
        };
    }
}
