using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;

namespace MFTL.Collections.Api.Configuration;

public class OpenApiConfigurationOptions : DefaultOpenApiConfigurationOptions
{
    public override OpenApiInfo Info { get; set; } = new OpenApiInfo
    {
        Title = "MFTL Collections API",
        Version = "v1",
        Description = "Multi-tenant contribution platform API."
    };

    public override OpenApiVersionType OpenApiVersion { get; set; } = OpenApiVersionType.V3;
}
