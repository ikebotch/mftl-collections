using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;
using MFTL.Collections.Infrastructure.Configuration;

namespace MFTL.Collections.Api.Functions.Docs;

public class ScalarFunction(IOptions<ScalarOptions> options)
{
    [Function("ScalarUi")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "docs/scalar")] HttpRequestData req)
    {
        // Point to the official Functions OpenAPI endpoint
        var swaggerUrl = "/api/openapi/v3.json";
        
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

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/html; charset=utf-8");
        await response.WriteStringAsync(html);
        return response;
    }
}
