using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Infrastructure.Services;

public class DataSeederService(IServiceScopeFactory scopeFactory, ILogger<DataSeederService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Data seeding starting...");
        
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CollectionsDbContext>();
        
        if (context.Database.IsInMemory())
        {
            await SeedAsync(context);
        }
        
        logger.LogInformation("Data seeding completed.");
    }

    private async Task SeedAsync(CollectionsDbContext context)
    {
        if (context.Tenants.Any()) return;

        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Verification Tenant",
            Identifier = "verify",
            IsActive = true
        };
        context.Tenants.Add(tenant);

        var eventId = Guid.NewGuid();
        var @event = new Event
        {
            Id = eventId,
            TenantId = tenantId,
            Title = "Live Verification Event",
            Description = "Support our community care collection.",
            Slug = "verify-event",
            IsActive = true
        };
        context.Events.Add(@event);

        var fund1 = new RecipientFund
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventId = eventId,
            Name = "Medical Support",
            Description = "Urgent treatment and aftercare.",
            TargetAmount = 5000
        };
        var fund2 = new RecipientFund
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventId = eventId,
            Name = "School Supplies",
            Description = "Fund fees and transport.",
            TargetAmount = 2500
        };
        context.RecipientFunds.Add(fund1);
        context.RecipientFunds.Add(fund2);

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded Tenant: {TenantId}, Event: {Slug}", tenantId, @event.Slug);
    }
}
