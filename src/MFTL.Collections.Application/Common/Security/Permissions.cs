namespace MFTL.Collections.Application.Common.Security;

/// <summary>
/// Centralised permission key constants.
/// Rules:
///   - Permission = what action the user can perform.
///   - Scope     = where the user can perform it (enforced by CanAccessAsync / EF query filters).
///   - Wildcard "*" is ONLY granted to Platform Admin.
///   - Module wildcards "resource.*" are NOT granted in role mappings; use granular keys.
/// </summary>
public static class Permissions
{
    /// <summary>Global wildcard — Platform Admin only.</summary>
    public const string All = "*";

    public static class Dashboard
    {
        public const string View  = "dashboard.view";
        /// <summary>Legacy — prefer Dashboard.View. Kept for backward compatibility.</summary>
        public const string Admin = "dashboard.admin";
    }

    public static class Organisations
    {
        public const string View   = "organisations.view";
        public const string Create = "organisations.create";   // Platform Admin only
        public const string Update = "organisations.update";
        public const string Delete = "organisations.delete";   // Platform Admin only
    }

    public static class Branches
    {
        public const string View   = "branches.view";
        public const string Create = "branches.create";
        public const string Update = "branches.update";
        public const string Delete = "branches.delete";
    }

    public static class Users
    {
        public const string View         = "users.view";
        public const string Invite       = "users.invite";
        public const string Update       = "users.update";
        public const string Suspend      = "users.suspend";
        public const string RolesAssign  = "users.roles.assign";
        public const string ScopesAssign = "users.scopes.assign";
        public const string ScopesRevoke = "users.scopes.revoke";
        /// <summary>Legacy alias — kept for backward compat. Prefer ScopesAssign.</summary>
        public const string AssignScope  = "users.assign-scope";
        /// <summary>Legacy alias — kept for backward compat. Prefer Users.Invite.</summary>
        public const string Resend       = "users.resend";
    }

    public static class Events
    {
        public const string View        = "events.view";
        public const string Create      = "events.create";
        public const string Update      = "events.update";
        public const string Delete      = "events.delete";
        public const string AssignStaff = "events.assign-staff";
    }

    public static class RecipientFunds
    {
        public const string View   = "recipient-funds.view";
        public const string Create = "recipient-funds.create";
        public const string Update = "recipient-funds.update";
        public const string Delete = "recipient-funds.delete";
    }

    public static class Contributions
    {
        public const string View    = "contributions.view";
        public const string Create  = "contributions.create";
        public const string Update  = "contributions.update";
        public const string Reverse = "contributions.reverse";
        /// <summary>Legacy — kept for backward compat.</summary>
        public const string Settle  = "contributions.settle";
    }

    public static class Receipts
    {
        public const string View   = "receipts.view";
        public const string Resend = "receipts.resend";
    }

    public static class Payments
    {
        public const string View          = "payments.view";
        public const string Initiate      = "payments.initiate";
        public const string Refund        = "payments.refund";
        public const string RefundApprove = "payments.refund.approve";
        public const string Reconcile     = "payments.reconcile";
        public const string RefundsView   = "payments.refunds.view";
        /// <summary>Legacy alias — kept for backward compat. Prefer Payments.View.</summary>
        public const string Retry         = "payments.retry";
    }

    public static class Settlements
    {
        public const string View      = "settlements.view";
        public const string Submit    = "settlements.submit";
        public const string Approve   = "settlements.approve";
        public const string Reject    = "settlements.reject";
        public const string Reconcile = "settlements.reconcile";
    }

    public static class Reports
    {
        public const string View   = "reports.view";
        public const string Export = "reports.export";
    }

    public static class Donors
    {
        public const string View   = "donors.view";
        public const string Update = "donors.update";
    }

    public static class NotificationTemplates
    {
        public const string View          = "notification-templates.view";
        public const string Create        = "notification-templates.create";
        public const string Update        = "notification-templates.update";
        public const string Delete        = "notification-templates.delete";
        public const string Test          = "notification-templates.test";
        public const string SystemManage  = "notification-templates.system.manage";  // Platform Admin only

        // Legacy aliases — kept for backward compat (mapped in PermissionBootstrapper)
        public const string Preview  = "notification-templates.preview";
        public const string SendTest = "notification-templates.send-test";
    }

    public static class Notifications
    {
        public const string View  = "notifications.view";
        public const string Retry = "notifications.retry";
    }

    public static class Settings
    {
        public const string View   = "settings.view";
        public const string Update = "settings.update";
    }

    public static class Platform
    {
        public const string Manage = "platform.manage";  // Platform Admin only
    }

    // ─── Legacy aliases for old code that references these ───────────────────

    /// <summary>Legacy — use granular Notifications.* and NotificationTemplates.* instead.</summary>
    [System.Obsolete("Use NotificationTemplates.* or Notifications.* instead.")]
    public static class NotificationsLegacy
    {
        public const string All      = "notification-templates.*";
        public const string Manage   = "notification-templates.manage";
    }

    /// <summary>Legacy — use granular Donors.* instead.</summary>
    [System.Obsolete("Use Donors.View / Donors.Update instead.")]
    public static class Donations
    {
        public const string View = "donations.view";
    }
}
