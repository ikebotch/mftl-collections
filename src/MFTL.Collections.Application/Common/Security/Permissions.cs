namespace MFTL.Collections.Application.Common.Security;

public static class Permissions
{
    public const string All = "*";

    public static class Dashboard
    {
        public const string View = "dashboard.view";
        public const string Admin = "dashboard.admin";
    }

    public static class Organisations
    {
        public const string View = "organisations.view";
        public const string Create = "organisations.create";
        public const string Update = "organisations.update";
        public const string Manage = "organisations.manage";
    }

    public static class Users
    {
        public const string All = "users.*";
        public const string View = "users.view";
        public const string Create = "users.create";
        public const string Update = "users.update";
        public const string Manage = "users.manage";
        public const string Invite = "users.invite";
        public const string Suspend = "users.suspend";
        public const string Resend = "users.resend";
        public const string AssignScope = "users.assign-scope";
    }

    public static class Events
    {
        public const string All = "events.*";
        public const string View = "events.view";
        public const string Create = "events.create";
        public const string Update = "events.update";
        public const string Delete = "events.delete";
        public const string Manage = "events.manage";
    }

    public static class Contributions
    {
        public const string All = "contributions.*";
        public const string View = "contributions.view";
        public const string Create = "contributions.create";
        public const string Update = "contributions.update";
        public const string Settle = "contributions.settle";
        public const string Manage = "contributions.manage";
    }

    public static class Donors
    {
        public const string All = "donors.*";
        public const string View = "donors.view";
        public const string Create = "donors.create";
        public const string Update = "donors.update";
        public const string Manage = "donors.manage";
    }

    public static class Reports
    {
        public const string View = "reports.view";
        public const string Export = "reports.export";
    }

    public static class Settlements
    {
        public const string All = "settlements.*";
        public const string View = "settlements.view";
        public const string Manage = "settlements.manage";
    }

    public static class Branches
    {
        public const string All = "branches.*";
        public const string View = "branches.view";
        public const string Create = "branches.create";
        public const string Update = "branches.update";
        public const string Delete = "branches.delete";
        public const string Manage = "branches.manage";
    }

    public static class Settings
    {
        public const string All = "settings.*";
        public const string View = "settings.view";
        public const string Update = "settings.update";
    }

    public static class Notifications
    {
        public const string All = "notification-templates.*";
        public const string View = "notification-templates.view";
        public const string Create = "notification-templates.create";
        public const string Update = "notification-templates.update";
        public const string Preview = "notification-templates.preview";
        public const string SendTest = "notification-templates.send-test";
        public const string Manage = "notification-templates.manage";
    }

    public static class Donations
    {
        public const string View = "donations.view";
    }

    public static class Payments
    {
        public const string All = "payments.*";
        public const string View = "payments.view";
        public const string Retry = "payments.retry";
        public const string Manage = "payments.manage";
    }
}
