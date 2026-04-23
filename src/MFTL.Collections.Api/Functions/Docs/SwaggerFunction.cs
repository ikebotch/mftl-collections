using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Swashbuckle.AspNetCore.Swagger;

namespace MFTL.Collections.Api.Functions.Docs;

public class SwaggerFunction(ISwaggerProvider swaggerProvider)
{
    [Function("SwaggerJson")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger/v1/swagger.json")] HttpRequest req)
    {
        var swagger = swaggerProvider.GetSwagger("v1");
        
        return new OkObjectResult(swagger);
    }
}
