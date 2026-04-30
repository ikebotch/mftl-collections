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
using Microsoft.EntityFrameworkCore.Diagnostics;

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
        services.AddScoped<ISaveChangesInterceptor, OutboxInterceptor>();
        
        services.AddScoped<FunctionHttpRequestAccessor>();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        
        services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
        services.AddScoped<IPaymentWebhookProcessor, PaymentWebhookProcessor>();
        services.AddScoped<IContributionSettlementService, ContributionSettlementService>();
        services.AddScoped<IReceiptNumberGenerator, ReceiptNumberGenerator>();
        services.AddScoped<IOutboxService, OutboxService>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        services.AddScoped<ITemplateRenderer, TemplateRenderer>();
        services.AddScoped<INotificationTemplateResolver, NotificationTemplateResolver>();
        services.AddHttpClient(nameof(GiantSmsService));
        services.AddScoped<ISmsService, GiantSmsService>();
        
        services.AddScoped<IPaymentProvider, StripePaymentProvider>();
        services.AddScoped<IPaymentProvider, PaystackPaymentProvider>();
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
            services.AddDbContext<CollectionsDbContext>((provider, options) =>
            {
                options.UseNpgsql(dbOptions.ConnectionString);
                options.AddInterceptors(provider.GetServices<ISaveChangesInterceptor>());
            });
            
            services.AddScoped<IApplicationDbContext>(provider => 
                provider.GetRequiredService<CollectionsDbContext>());
            services.AddHostedService<ReceiptSchemaBootstrapper>();
            services.AddHostedService<NotificationSchemaBootstrapper>();
            services.AddHostedService<PermissionBootstrapper>();
        }

        // Additional services for payments and dashboards
        services.AddScoped<IPaymentStateService, PaymentStateService>();
        services.AddScoped<IDashboardProjectionUpdater, DashboardProjectionUpdater>();

        // Email: use SendGrid when API key is configured; fall back to MockEmailService
        services.Configure<SendGridOptions>(configuration.GetSection(SendGridOptions.SectionName));
        var sendGridApiKey = configuration[$"{SendGridOptions.SectionName}:ApiKey"];
        if (!string.IsNullOrWhiteSpace(sendGridApiKey))
        {
            services.AddScoped<IEmailService, SendGridEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, MockEmailService>();
        }

        return services;
    }
}
