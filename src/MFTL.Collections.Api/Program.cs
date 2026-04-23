using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using MFTL.Collections.Application.DependencyInjection;
using MFTL.Collections.Infrastructure.DependencyInjection;
using MFTL.Collections.Api.Middleware;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(worker =>
    {
        worker.UseMiddleware<ExceptionHandlingMiddleware>();
        worker.UseMiddleware<TenantResolutionMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplication();
        services.AddInfrastructure(context.Configuration);
        
        // Register custom OpenAPI configuration with explicit type name
        services.AddSingleton<IOpenApiConfigurationOptions, MFTL.Collections.Api.Configuration.OpenApiConfigurationOptions>();
    })
    .Build();

host.Run();
