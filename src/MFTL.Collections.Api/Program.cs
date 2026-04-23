using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MFTL.Collections.Application.DependencyInjection;
using MFTL.Collections.Infrastructure.DependencyInjection;
using MFTL.Collections.Api.Middleware;
using MFTL.Collections.Api.Extensions;

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
        
        services.AddCollectionsOpenApi();
    })
    .Build();

host.Run();
