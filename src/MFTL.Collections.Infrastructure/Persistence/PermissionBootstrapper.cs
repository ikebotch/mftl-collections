using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Infrastructure.Persistence;

/// <summary>
/// Seeds the Permission catalogue and RolePermission mappings on startup.
/// </summary>
public sealed class PermissionBootstrapper(
    IServiceScopeFactory scopeFactory,
    ILogger<PermissionBootstrapper> logger) : IHostedService
{
    private static readonly string[] ManagedRoles = AppRoles.All.ToArray();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try 
        {
            logger.LogInformation("Starting permission bootstrap background task...");

            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            // ── 1. Seed Permission catalogue ──────────────────────────────────────
            var existingKeys = await dbContext.Permissions
                .Select(p => p.Key)
                .ToListAsync(cancellationToken);

            var catalogue = BuildPermissionCatalogue();
            foreach (var perm in catalogue)
            {
                if (!existingKeys.Contains(perm.Key))
                {
                    dbContext.Permissions.Add(perm);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // ── 2. Idempotent role mapping ────────────────────────────────────────
            logger.LogInformation("Cleaning up stale permissions for managed roles...");
            var stale = await dbContext.RolePermissions
                .IgnoreQueryFilters()
                .Where(rp => ManagedRoles.Contains(rp.RoleName))
                .ToListAsync(cancellationToken);

            foreach (var row in stale)
            {
                dbContext.RolePermissions.Remove(row);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Removed {Count} stale role-permission mappings.", stale.Count);

            // ── 3. Seed granular role mappings ────────────────────────────────────
            var roleMappings = BuildRoleMappings();
            var addedCount = 0;
            foreach (var (role, permissions) in roleMappings)
            {
                AppRoles.Guard(role);
                foreach (var key in permissions)
                {
                    dbContext.RolePermissions.Add(new RolePermission
                    {
                        RoleName = role,
                        PermissionKey = key,
                    });
                    addedCount++;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Permission bootstrap completed. Added {Count} mappings across {RolesCount} roles.", addedCount, roleMappings.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Permission bootstrap failed at step: {Message}", ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static List<Permission> BuildPermissionCatalogue()
    {
        var catalogue = new List<Permission>();
        
        // Dashboard
        catalogue.Add(new Permission { Key = Permissions.Dashboard.View, Group = "Dashboard", Description = "Allows viewing of dashboard metrics." });
        
        // Organisations
        catalogue.Add(new Permission { Key = Permissions.Organisations.View, Group = "Organisations", Description = "Allows viewing organisation lists." });
        catalogue.Add(new Permission { Key = Permissions.Organisations.Create, Group = "Organisations", Description = "Allows creating new organisations (Platform Admin)." });
        catalogue.Add(new Permission { Key = Permissions.Organisations.Update, Group = "Organisations", Description = "Allows updating organisation details." });
        catalogue.Add(new Permission { Key = Permissions.Organisations.Delete, Group = "Organisations", Description = "Allows deleting organisations (Platform Admin)." });

        // Branches
        catalogue.Add(new Permission { Key = Permissions.Branches.View, Group = "Branches", Description = "Allows viewing of branches." });
        catalogue.Add(new Permission { Key = Permissions.Branches.Create, Group = "Branches", Description = "Allows creating new branches." });
        catalogue.Add(new Permission { Key = Permissions.Branches.Update, Group = "Branches", Description = "Allows updating branch details." });
        catalogue.Add(new Permission { Key = Permissions.Branches.Delete, Group = "Branches", Description = "Allows deleting branches." });

        // Users
        catalogue.Add(new Permission { Key = Permissions.Users.View, Group = "Users", Description = "Allows viewing of users." });
        catalogue.Add(new Permission { Key = Permissions.Users.Invite, Group = "Users", Description = "Allows inviting new users." });
        catalogue.Add(new Permission { Key = Permissions.Users.Update, Group = "Users", Description = "Allows updating user profiles." });
        catalogue.Add(new Permission { Key = Permissions.Users.Suspend, Group = "Users", Description = "Allows suspending users." });
        catalogue.Add(new Permission { Key = Permissions.Users.RolesAssign, Group = "Users", Description = "Allows assigning system roles (Platform Admin)." });
        catalogue.Add(new Permission { Key = Permissions.Users.ScopesAssign, Group = "Users", Description = "Allows assigning tenant/branch/event scopes." });
        catalogue.Add(new Permission { Key = Permissions.Users.ScopesRevoke, Group = "Users", Description = "Allows revoking user scope assignments." });

        // Events
        catalogue.Add(new Permission { Key = Permissions.Events.View, Group = "Events", Description = "Allows viewing of events." });
        catalogue.Add(new Permission { Key = Permissions.Events.Create, Group = "Events", Description = "Allows creating new events." });
        catalogue.Add(new Permission { Key = Permissions.Events.Update, Group = "Events", Description = "Allows updating event details." });
        catalogue.Add(new Permission { Key = Permissions.Events.Delete, Group = "Events", Description = "Allows deleting events." });
        catalogue.Add(new Permission { Key = Permissions.Events.AssignStaff, Group = "Events", Description = "Allows assigning staff to events." });

        // Recipient Funds
        catalogue.Add(new Permission { Key = Permissions.RecipientFunds.View, Group = "Funds", Description = "Allows viewing of recipient funds." });
        catalogue.Add(new Permission { Key = Permissions.RecipientFunds.Create, Group = "Funds", Description = "Allows creating new funds." });
        catalogue.Add(new Permission { Key = Permissions.RecipientFunds.Update, Group = "Funds", Description = "Allows updating fund details." });
        catalogue.Add(new Permission { Key = Permissions.RecipientFunds.Delete, Group = "Funds", Description = "Allows deleting funds." });

        // Contributions
        catalogue.Add(new Permission { Key = Permissions.Contributions.View, Group = "Contributions", Description = "Allows viewing contribution logs." });
        catalogue.Add(new Permission { Key = Permissions.Contributions.Create, Group = "Contributions", Description = "Allows recording new contributions." });
        catalogue.Add(new Permission { Key = Permissions.Contributions.Update, Group = "Contributions", Description = "Allows updating contribution details." });
        catalogue.Add(new Permission { Key = Permissions.Contributions.Reverse, Group = "Contributions", Description = "Allows reversing contribution transactions." });

        // Receipts
        catalogue.Add(new Permission { Key = Permissions.Receipts.View, Group = "Receipts", Description = "Allows viewing issued receipts." });
        catalogue.Add(new Permission { Key = Permissions.Receipts.Resend, Group = "Receipts", Description = "Allows resending receipts to contributors." });

        // Payments
        catalogue.Add(new Permission { Key = Permissions.Payments.View, Group = "Payments", Description = "Allows viewing payment transactions." });
        catalogue.Add(new Permission { Key = Permissions.Payments.Initiate, Group = "Payments", Description = "Allows initiating external payments." });
        catalogue.Add(new Permission { Key = Permissions.Payments.Refund, Group = "Payments", Description = "Allows initiating refunds." });
        catalogue.Add(new Permission { Key = Permissions.Payments.RefundApprove, Group = "Payments", Description = "Allows approving refund requests." });
        catalogue.Add(new Permission { Key = Permissions.Payments.Reconcile, Group = "Payments", Description = "Allows manual payment reconciliation." });
        catalogue.Add(new Permission { Key = Permissions.Payments.RefundsView, Group = "Payments", Description = "Allows viewing refund logs." });

        // Settlements
        catalogue.Add(new Permission { Key = Permissions.Settlements.View, Group = "Settlements", Description = "Allows viewing cash settlements." });
        catalogue.Add(new Permission { Key = Permissions.Settlements.Submit, Group = "Settlements", Description = "Allows submitting settlements for approval." });
        catalogue.Add(new Permission { Key = Permissions.Settlements.Approve, Group = "Settlements", Description = "Allows approving cash handovers." });
        catalogue.Add(new Permission { Key = Permissions.Settlements.Reject, Group = "Settlements", Description = "Allows rejecting settlement submissions." });
        catalogue.Add(new Permission { Key = Permissions.Settlements.Reconcile, Group = "Settlements", Description = "Allows settlement reconciliation." });

        // Reports
        catalogue.Add(new Permission { Key = Permissions.Reports.View, Group = "Reports", Description = "Allows viewing of analytical reports." });
        catalogue.Add(new Permission { Key = Permissions.Reports.Export, Group = "Reports", Description = "Allows exporting report data." });

        // Donors
        catalogue.Add(new Permission { Key = Permissions.Donors.View, Group = "Donors", Description = "Allows viewing donor profiles." });
        catalogue.Add(new Permission { Key = Permissions.Donors.Update, Group = "Donors", Description = "Allows updating donor details." });

        // Notification Templates
        catalogue.Add(new Permission { Key = Permissions.NotificationTemplates.View, Group = "Templates", Description = "Allows viewing notification templates." });
        catalogue.Add(new Permission { Key = Permissions.NotificationTemplates.Create, Group = "Templates", Description = "Allows creating new templates." });
        catalogue.Add(new Permission { Key = Permissions.NotificationTemplates.Update, Group = "Templates", Description = "Allows updating existing templates." });
        catalogue.Add(new Permission { Key = Permissions.NotificationTemplates.Delete, Group = "Templates", Description = "Allows deleting templates." });
        catalogue.Add(new Permission { Key = Permissions.NotificationTemplates.Test, Group = "Templates", Description = "Allows sending test notifications." });
        catalogue.Add(new Permission { Key = Permissions.NotificationTemplates.SystemManage, Group = "Templates", Description = "Allows managing core system templates (Platform Admin)." });

        // Notifications
        catalogue.Add(new Permission { Key = Permissions.Notifications.View, Group = "Notifications", Description = "Allows viewing notification logs." });
        catalogue.Add(new Permission { Key = Permissions.Notifications.Retry, Group = "Notifications", Description = "Allows retrying failed notifications." });

        // Settings
        catalogue.Add(new Permission { Key = Permissions.Settings.View, Group = "Settings", Description = "Allows viewing tenant settings." });
        catalogue.Add(new Permission { Key = Permissions.Settings.Update, Group = "Settings", Description = "Allows updating tenant settings." });

        // Platform
        catalogue.Add(new Permission { Key = Permissions.Platform.Manage, Group = "Platform", Description = "Allows global system administration." });

        return catalogue;
    }

    private static Dictionary<string, List<string>> BuildRoleMappings()
    {
        var adminPermissions = new List<string>
        {
            Permissions.Dashboard.View,
            Permissions.Organisations.View,
            Permissions.Organisations.Update,
            Permissions.Branches.View,
            Permissions.Branches.Create,
            Permissions.Branches.Update,
            Permissions.Users.View,
            Permissions.Users.Invite,
            Permissions.Users.Update,
            Permissions.Users.Suspend,
            Permissions.Users.ScopesAssign,
            Permissions.Users.ScopesRevoke,
            Permissions.Events.View,
            Permissions.Events.Create,
            Permissions.Events.Update,
            Permissions.Events.Delete,
            Permissions.Events.AssignStaff,
            Permissions.RecipientFunds.View,
            Permissions.RecipientFunds.Create,
            Permissions.RecipientFunds.Update,
            Permissions.RecipientFunds.Delete,
            Permissions.Contributions.View,
            Permissions.Receipts.View,
            Permissions.Receipts.Resend,
            Permissions.Payments.View,
            Permissions.Payments.RefundsView,
            Permissions.Settlements.View,
            Permissions.Reports.View,
            Permissions.Reports.Export,
            Permissions.Donors.View,
            Permissions.Donors.Update,
            Permissions.NotificationTemplates.View,
            Permissions.NotificationTemplates.Create,
            Permissions.NotificationTemplates.Update,
            Permissions.NotificationTemplates.Test,
            Permissions.Notifications.View,
            Permissions.Notifications.Retry,
            Permissions.Settings.View,
            Permissions.Settings.Update,
        };

        return new Dictionary<string, List<string>>
        {
            [AppRoles.PlatformAdmin] = ["*"],

            [AppRoles.OrganisationAdmin] = adminPermissions,

            [AppRoles.BranchAdmin] =
            [
                Permissions.Dashboard.View,
                Permissions.Branches.View,
                Permissions.Branches.Update,
                Permissions.Users.View,
                Permissions.Users.Invite,
                Permissions.Users.Update,
                Permissions.Events.View,
                Permissions.Events.Create,
                Permissions.Events.Update,
                Permissions.Events.AssignStaff,
                Permissions.RecipientFunds.View,
                Permissions.RecipientFunds.Create,
                Permissions.RecipientFunds.Update,
                Permissions.Contributions.View,
                Permissions.Receipts.View,
                Permissions.Receipts.Resend,
                Permissions.Donors.View,
                Permissions.NotificationTemplates.View,
                Permissions.NotificationTemplates.Update,
                Permissions.NotificationTemplates.Test,
                Permissions.Notifications.View,
                Permissions.Reports.View,
            ],

            [AppRoles.FinanceAdmin] =
            [
                Permissions.Dashboard.View,
                Permissions.Contributions.View,
                Permissions.Receipts.View,
                Permissions.Receipts.Resend,
                Permissions.Payments.View,
                Permissions.Payments.Refund,
                Permissions.Payments.RefundApprove,
                Permissions.Payments.Reconcile,
                Permissions.Payments.RefundsView,
                Permissions.Settlements.View,
                Permissions.Settlements.Submit,
                Permissions.Settlements.Approve,
                Permissions.Settlements.Reject,
                Permissions.Settlements.Reconcile,
                Permissions.Reports.View,
                Permissions.Reports.Export,
                Permissions.Notifications.View,
            ],

            [AppRoles.EventManager] =
            [
                Permissions.Dashboard.View,
                Permissions.Events.View,
                Permissions.Events.Update,
                Permissions.RecipientFunds.View,
                Permissions.RecipientFunds.Create,
                Permissions.RecipientFunds.Update,
                Permissions.Contributions.View,
                Permissions.Receipts.View,
                Permissions.Reports.View,
            ],

            [AppRoles.Collector] =
            [
                Permissions.Dashboard.View,
                Permissions.Events.View,
                Permissions.RecipientFunds.View,
                Permissions.Contributions.Create,
                Permissions.Payments.Initiate,
                Permissions.Receipts.View,
            ],

            [AppRoles.NotificationManager] =
            [
                Permissions.Dashboard.View,
                Permissions.NotificationTemplates.View,
                Permissions.NotificationTemplates.Create,
                Permissions.NotificationTemplates.Update,
                Permissions.NotificationTemplates.Delete,
                Permissions.NotificationTemplates.Test,
                Permissions.Notifications.View,
                Permissions.Notifications.Retry,
            ],

            [AppRoles.Viewer] =
            [
                Permissions.Dashboard.View,
                Permissions.Events.View,
                Permissions.Contributions.View,
                Permissions.Receipts.View,
                Permissions.Reports.View,
            ]
        };
    }
}
