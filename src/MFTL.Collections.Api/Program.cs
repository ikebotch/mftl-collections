using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MFTL.Collections.Application.DependencyInjection;
using MFTL.Collections.Infrastructure.DependencyInjection;
using MFTL.Collections.Api.Middleware;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.UseMiddleware<ExceptionHandlingMiddleware>();
        worker.UseMiddleware<TenantResolutionMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplication();
        services.AddInfrastructure(context.Configuration);
    })
    .Build();

host.Run();
