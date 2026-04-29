using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MFTL.Collections.Application.DependencyInjection;
using MFTL.Collections.Infrastructure.DependencyInjection;
using MFTL.Collections.Api.Middleware;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Api.Infrastructure.Json;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(worker =>
    {
        worker.UseMiddleware<ExceptionHandlingMiddleware>();
        worker.UseMiddleware<AuthenticationMiddleware>();
        worker.UseMiddleware<EndpointAccessPolicyMiddleware>();
        worker.UseMiddleware<TenantResolutionMiddleware>();
        worker.UseMiddleware<UserProvisioningMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddScoped<EndpointAccessPolicyMiddleware>();
        services.AddApplication();
        services.AddInfrastructure(context.Configuration);
        services.AddHttpContextAccessor();
        
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            options.SerializerOptions.Converters.Add(new FlexibleDateTimeOffsetConverter());
        });

        services.AddCollectionsOpenApi();
    })
    .Build();

host.Run();
