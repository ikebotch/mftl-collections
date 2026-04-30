using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Common.Interfaces;
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
            
            new() { Key = Permissions.Dashboard.View, Description = "View basic dashboard", Group = "Dashboard" },
            new() { Key = Permissions.Dashboard.Admin, Description = "View administrative dashboard", Group = "Dashboard" },
            
            new() { Key = Permissions.Organisations.View, Description = "View organization details", Group = "System" },
            new() { Key = Permissions.Organisations.Create, Description = "Create new organizations", Group = "System" },
            new() { Key = Permissions.Organisations.Update, Description = "Update organization details", Group = "System" },
            new() { Key = Permissions.Organisations.Manage, Description = "Full management of organizations", Group = "System" },

            new() { Key = Permissions.Events.All, Description = "Manage all events (Wildcard)", Group = "Events" },
            new() { Key = Permissions.Events.View, Description = "View events and campaigns", Group = "Events" },
            new() { Key = Permissions.Events.Create, Description = "Create new events", Group = "Events" },
            new() { Key = Permissions.Events.Update, Description = "Update existing events", Group = "Events" },
            new() { Key = Permissions.Events.Delete, Description = "Delete or deactivate events", Group = "Events" },
            new() { Key = Permissions.Events.Manage, Description = "Manage event operations", Group = "Events" },

            new() { Key = Permissions.Contributions.All, Description = "Manage all contributions (Wildcard)", Group = "Contributions" },
            new() { Key = Permissions.Contributions.View, Description = "View contributions", Group = "Contributions" },
            new() { Key = Permissions.Contributions.Create, Description = "Record new contributions", Group = "Contributions" },
            new() { Key = Permissions.Contributions.Update, Description = "Edit contribution details", Group = "Contributions" },
            new() { Key = Permissions.Contributions.Settle, Description = "Settle/Reconcile contributions", Group = "Contributions" },
            new() { Key = Permissions.Contributions.Manage, Description = "Manage contribution flows", Group = "Contributions" },

            new() { Key = Permissions.Donors.All, Description = "Manage all donor profiles (Wildcard)", Group = "Donors" },
            new() { Key = Permissions.Donors.View, Description = "View donor profiles", Group = "Donors" },
            new() { Key = Permissions.Donors.Create, Description = "Register new donors", Group = "Donors" },
            new() { Key = Permissions.Donors.Update, Description = "Update donor profiles", Group = "Donors" },
            new() { Key = Permissions.Donors.Manage, Description = "Manage donor database", Group = "Donors" },

            new() { Key = Permissions.Reports.View, Description = "View analytical reports", Group = "Reports" },
            new() { Key = Permissions.Reports.Export, Description = "Export report data", Group = "Reports" },

            new() { Key = Permissions.Settlements.All, Description = "Manage financial settlements (Wildcard)", Group = "Settlements" },
            new() { Key = Permissions.Settlements.View, Description = "View financial settlements", Group = "Settlements" },
            new() { Key = Permissions.Settlements.Manage, Description = "Execute financial settlements", Group = "Settlements" },

            new() { Key = Permissions.Branches.All, Description = "Manage organization branches (Wildcard)", Group = "Branches" },
            new() { Key = Permissions.Branches.View, Description = "View organization branches", Group = "Branches" },
            new() { Key = Permissions.Branches.Create, Description = "Create new branches", Group = "Branches" },
            new() { Key = Permissions.Branches.Update, Description = "Update branch details", Group = "Branches" },
            new() { Key = Permissions.Branches.Delete, Description = "Delete or deactivate branches", Group = "Branches" },
            new() { Key = Permissions.Branches.Manage, Description = "Full management of branches", Group = "Branches" },

            new() { Key = Permissions.Users.All, Description = "Manage system users (Wildcard)", Group = "Users" },
            new() { Key = Permissions.Users.View, Description = "View system users", Group = "Users" },
            new() { Key = Permissions.Users.Create, Description = "Add new system users", Group = "Users" },
            new() { Key = Permissions.Users.Update, Description = "Update user profiles", Group = "Users" },
            new() { Key = Permissions.Users.Manage, Description = "Manage user access and roles", Group = "Users" },
            new() { Key = Permissions.Users.Invite, Description = "Invite new users via email", Group = "Users" },
            new() { Key = Permissions.Users.Suspend, Description = "Suspend user accounts", Group = "Users" },
            new() { Key = Permissions.Users.Resend, Description = "Resend user invitations", Group = "Users" },
            new() { Key = Permissions.Users.AssignScope, Description = "Assign scopes to users", Group = "Users" },

            new() { Key = Permissions.Settings.All, Description = "Manage system settings (Wildcard)", Group = "Settings" },
            new() { Key = Permissions.Settings.View, Description = "View system settings", Group = "Settings" },
            new() { Key = Permissions.Settings.Update, Description = "Update system settings", Group = "Settings" },

            new() { Key = Permissions.Notifications.All, Description = "Manage communication templates (Wildcard)", Group = "Notifications" },
            new() { Key = Permissions.Notifications.View, Description = "View communication templates", Group = "Notifications" },
            new() { Key = Permissions.Notifications.Create, Description = "Create communication templates", Group = "Notifications" },
            new() { Key = Permissions.Notifications.Update, Description = "Update communication templates", Group = "Notifications" },
            new() { Key = Permissions.Notifications.Preview, Description = "Preview rendered templates", Group = "Notifications" },
            new() { Key = Permissions.Notifications.SendTest, Description = "Send test notifications", Group = "Notifications" },
            new() { Key = Permissions.Notifications.Manage, Description = "Full management of notifications", Group = "Notifications" },

            new() { Key = Permissions.Donations.View, Description = "View donation history", Group = "Donations" },

            new() { Key = Permissions.Payments.All, Description = "Manage all payments (Wildcard)", Group = "Payments" },
            new() { Key = Permissions.Payments.View, Description = "View payment records", Group = "Payments" },
            new() { Key = Permissions.Payments.Retry, Description = "Retry failed payments", Group = "Payments" },
            new() { Key = Permissions.Payments.Manage, Description = "Manage payment orchestrations", Group = "Payments" }
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
                Permissions.Dashboard.Admin,
                Permissions.Events.Manage,
                Permissions.Contributions.Manage,
                Permissions.Donors.Manage,
                Permissions.Reports.View,
                Permissions.Reports.Export,
                Permissions.Settlements.Manage,
                Permissions.Branches.Manage,
                Permissions.Users.Manage,
                Permissions.Organisations.View,
                Permissions.Settings.All,
                Permissions.Notifications.Manage,
                Permissions.Donations.View,
                Permissions.Payments.View
            } },
            { "Tenant Owner", new[] { 
                Permissions.Dashboard.Admin,
                Permissions.Events.Manage,
                Permissions.Contributions.Manage,
                Permissions.Donors.Manage,
                Permissions.Reports.View,
                Permissions.Reports.Export,
                Permissions.Settlements.Manage,
                Permissions.Branches.Manage,
                Permissions.Users.Manage,
                Permissions.Organisations.View,
                Permissions.Settings.All,
                Permissions.Notifications.Manage,
                Permissions.Donations.View,
                Permissions.Payments.View
            } },
            { "Branch Admin", new[] { 
                Permissions.Dashboard.View,
                Permissions.Events.Update,
                Permissions.Events.View,
                Permissions.Contributions.Settle,
                Permissions.Contributions.View,
                Permissions.Donors.View,
                Permissions.Reports.View,
                Permissions.Users.View,
                Permissions.Branches.View
            } },
            { "Event Manager", new[] { 
                Permissions.Dashboard.View,
                Permissions.Events.View,
                Permissions.Events.Update,
                Permissions.Contributions.View,
                Permissions.Reports.View,
                Permissions.Donors.View
            } },
            { "Finance Admin", new[] { 
                Permissions.Dashboard.View,
                Permissions.Contributions.View,
                Permissions.Payments.All, 
                Permissions.Settlements.Manage,
                Permissions.Reports.View,
                Permissions.Reports.Export,
                Permissions.Donations.View
            } },
            { "Accountant", new[] { 
                Permissions.Dashboard.View,
                Permissions.Contributions.View,
                Permissions.Settlements.View,
                Permissions.Reports.View,
                Permissions.Donations.View,
                Permissions.Payments.View
            } },
            { "Collector", new[] { 
                Permissions.Dashboard.View,
                Permissions.Contributions.Create,
                Permissions.Contributions.View,
                Permissions.Donations.View
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
