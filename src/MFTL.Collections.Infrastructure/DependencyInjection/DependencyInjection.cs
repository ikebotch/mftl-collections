using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Infrastructure.Configuration;
using MFTL.Collections.Infrastructure.Tenancy;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Infrastructure.Payments;
using MFTL.Collections.Infrastructure.Services;
using MFTL.Collections.Infrastructure.Identity;
using MFTL.Collections.Infrastructure.Identity.Auth0.Provisioning;
using MFTL.Collections.Infrastructure.Persistence.Interceptors;

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
        services.Configure<GiantSmsOptions>(configuration.GetSection(GiantSmsOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IScopeAccessService, ScopeAccessService>();
        services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();
        services.AddScoped<IAccessPolicyResolver, AccessPolicyResolver>();
        
        services.Configure<Auth0ProvisioningOptions>(options =>
        {
            options.Domain = configuration["Auth0:Domain"] ?? string.Empty;
            options.ManagementClientId = configuration["AUTH0_MANAGEMENT_CLIENT_ID"] ?? string.Empty;
            options.ManagementClientSecret = configuration["AUTH0_MANAGEMENT_CLIENT_SECRET"] ?? string.Empty;
            options.ManagementAudience = configuration["AUTH0_MANAGEMENT_AUDIENCE"] ?? (string.IsNullOrEmpty(options.Domain) ? string.Empty : $"https://{options.Domain}/api/v2/");
            options.ApiAudience = configuration["Auth0:Audience"] ?? string.Empty;
            options.WebhookSecret = configuration["AUTH0_WEBHOOK_SECRET"] ?? string.Empty;
        });
        services.AddScoped<Auth0ProvisioningService>();
        services.AddScoped<IAuth0Service>(sp => sp.GetRequiredService<Auth0ProvisioningService>());
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        
        services.AddScoped<FunctionHttpRequestAccessor>();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        
        services.AddScoped<BranchContext>();
        services.AddScoped<IBranchContext>(sp => sp.GetRequiredService<BranchContext>());
        services.AddScoped<HeaderBranchResolver>();
        
        services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
        services.AddScoped<IPaymentWebhookProcessor, PaymentWebhookProcessor>();
        services.AddScoped<IContributionSettlementService, ContributionSettlementService>();
        services.AddScoped<IReceiptNumberGenerator, ReceiptNumberGenerator>();
        
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
            services.AddSingleton<OutboxInterceptor>();
            services.AddDbContext<CollectionsDbContext>((sp, options) =>
            {
                options.UseNpgsql(dbOptions.ConnectionString)
                       .AddInterceptors(sp.GetRequiredService<OutboxInterceptor>());
            });
            
            services.AddScoped<IApplicationDbContext>(provider => 
                provider.GetRequiredService<CollectionsDbContext>());
        }

        // Additional services for payments and dashboards
        services.AddScoped<IPaymentStateService, PaymentStateService>();
        services.AddScoped<IDashboardProjectionUpdater, DashboardProjectionUpdater>();
        services.AddScoped<IEmailService, MockEmailService>();
        services.AddScoped<ISmsTemplateService, SmsTemplateService>();
        services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        
        services.AddHttpClient<ISmsService, GiantSmsService>();

        return services;
    }
}
