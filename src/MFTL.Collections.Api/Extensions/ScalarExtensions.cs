using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MFTL.Collections.Infrastructure.Configuration;

namespace MFTL.Collections.Api.Extensions;

public static class ScalarExtensions
{
    public static IServiceCollection AddCollectionsOpenApi(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            /*
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "MFTL Collections API",
                Version = "v1"
            });
            */
        });
        return services;
    }
}
