using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Configuration;
using MFTL.Collections.Infrastructure.Tenancy;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Infrastructure.Payments;
using MFTL.Collections.Infrastructure.Services;
using MFTL.Collections.Infrastructure.Identity;

namespace MFTL.Collections.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Auth0Options>(configuration.GetSection(Auth0Options.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<ApiVersionOptions>(configuration.GetSection(ApiVersionOptions.SectionName));
        services.Configure<OpenApiOptions>(configuration.GetSection(OpenApiOptions.SectionName));
        services.Configure<ScalarOptions>(configuration.GetSection(ScalarOptions.SectionName));
        services.Configure<TenantResolutionOptions>(configuration.GetSection(TenantResolutionOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IScopeAccessService, ScopeAccessService>();
        
        services.AddScoped<FunctionHttpRequestAccessor>();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        
        services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
        services.AddScoped<IPaymentWebhookProcessor, PaymentWebhookProcessor>();
        services.AddScoped<IContributionSettlementService, ContributionSettlementService>();
        
        services.AddScoped<IPaymentProvider, StripePaymentProvider>();
        services.AddScoped<ITenantResolver, HeaderTenantResolver>();
        services.AddScoped<ITenantResolver, HostTenantResolver>();
        services.AddScoped<CompositeTenantResolver>();

        var auth0Options = configuration.GetSection(Auth0Options.SectionName).Get<Auth0Options>();
        if (auth0Options != null)
        {
            services.AddAuth0Authentication(auth0Options);
        }

        var dbOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>();
        if (dbOptions != null && !string.IsNullOrEmpty(dbOptions.ConnectionString))
        {
            services.AddDbContext<CollectionsDbContext>(options =>
                options.UseNpgsql(dbOptions.ConnectionString));
            
            services.AddScoped<IApplicationDbContext>(provider => 
                provider.GetRequiredService<CollectionsDbContext>());
        }

        // Additional services for payments and dashboards
        services.AddScoped<IPaymentStateService, PaymentStateService>();
        services.AddScoped<IDashboardProjectionUpdater, DashboardProjectionUpdater>();

        return services;
    }
}
