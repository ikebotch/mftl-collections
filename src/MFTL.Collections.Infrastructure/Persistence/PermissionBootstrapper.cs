using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Infrastructure.Persistence;

public sealed class PermissionBootstrapper(
    IServiceProvider serviceProvider,
    ILogger<PermissionBootstrapper> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        logger.LogInformation("Starting permission bootstrap...");

        var existingPermissions = await dbContext.Permissions.Select(p => p.Key).ToListAsync(cancellationToken);
        
        var permissionsToSeed = new List<Permission>
        {
            new() { Key = Permissions.All, Description = "Full access to all system resources", Group = "System" },
            new() { Key = Permissions.Dashboard.View, Description = "View administrative dashboard", Group = "Dashboard" },
            new() { Key = Permissions.Events.All, Description = "Manage all events and campaigns", Group = "Events" },
            new() { Key = Permissions.Events.View, Description = "View events and campaigns", Group = "Events" },
            new() { Key = Permissions.Contributions.All, Description = "Manage all contributions", Group = "Contributions" },
            new() { Key = Permissions.Contributions.View, Description = "View contributions", Group = "Contributions" },
            new() { Key = Permissions.Contributions.Create, Description = "Record new contributions", Group = "Contributions" },
            new() { Key = Permissions.Donors.All, Description = "Manage all donor profiles", Group = "Donors" },
            new() { Key = Permissions.Donors.View, Description = "View donor profiles", Group = "Donors" },
            new() { Key = Permissions.Reports.View, Description = "View analytical reports", Group = "Reports" },
            new() { Key = Permissions.Settlements.All, Description = "Manage financial settlements", Group = "Settlements" },
            new() { Key = Permissions.Branches.All, Description = "Manage organization branches", Group = "Branches" },
            new() { Key = Permissions.Branches.View, Description = "View organization branches", Group = "Branches" },
            new() { Key = Permissions.Users.All, Description = "Manage system users and access", Group = "Users" },
            new() { Key = Permissions.Users.View, Description = "View system users", Group = "Users" },
            new() { Key = Permissions.Organisations.View, Description = "View organization details", Group = "System" },
            new() { Key = Permissions.Settings.All, Description = "Manage system and tenant settings", Group = "Settings" },
            new() { Key = Permissions.Notifications.All, Description = "Manage communication templates", Group = "Notifications" },
            new() { Key = Permissions.Donations.View, Description = "View donation history", Group = "Donations" },
            new() { Key = Permissions.Payments.All, Description = "Manage all payments", Group = "Payments" },
            new() { Key = Permissions.Payments.View, Description = "View payments", Group = "Payments" }
        };

        foreach (var permission in permissionsToSeed)
        {
            if (!existingPermissions.Contains(permission.Key))
            {
                dbContext.Permissions.Add(permission);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Seed Role Permissions
        var existingRolePermissions = await dbContext.RolePermissions.ToListAsync(cancellationToken);
        
        var roleMappings = new Dictionary<string, string[]>
        {
            { "Platform Admin", new[] { Permissions.All } },
            { "Organisation Admin", new[] { 
                Permissions.Dashboard.View, Permissions.Events.All, Permissions.Contributions.All, Permissions.Donors.All, 
                Permissions.Reports.View, Permissions.Settlements.All, Permissions.Branches.All, Permissions.Users.All, 
                Permissions.Organisations.View, Permissions.Settings.All, Permissions.Notifications.All,
                Permissions.Donations.View, Permissions.Payments.View
            } },
            { "Tenant Owner", new[] { 
                Permissions.Dashboard.View, Permissions.Events.All, Permissions.Contributions.All, Permissions.Donors.All, 
                Permissions.Reports.View, Permissions.Settlements.All, Permissions.Branches.All, Permissions.Users.All, 
                Permissions.Organisations.View, Permissions.Settings.All, Permissions.Notifications.All,
                Permissions.Donations.View, Permissions.Payments.View
            } },
            { "Branch Admin", new[] { 
                Permissions.Dashboard.View, Permissions.Events.View, Permissions.Contributions.All, Permissions.Donors.View, 
                Permissions.Reports.View, Permissions.Users.View, Permissions.Branches.View
            } },
            { "Collector", new[] { 
                Permissions.Contributions.Create, Permissions.Dashboard.View, Permissions.Contributions.View
            } },
            { "Finance Admin", new[] { 
                Permissions.Dashboard.View, Permissions.Contributions.View, Permissions.Payments.All, 
                Permissions.Settlements.All, Permissions.Reports.View, Permissions.Donations.View
            } }
        };

        foreach (var mapping in roleMappings)
        {
            foreach (var permKey in mapping.Value)
            {
                if (!existingRolePermissions.Any(rp => rp.RoleName == mapping.Key && rp.PermissionKey == permKey))
                {
                    dbContext.RolePermissions.Add(new RolePermission
                    {
                        RoleName = mapping.Key,
                        PermissionKey = permKey
                    });
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Permission bootstrap completed successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
