namespace MFTL.Collections.Application.Common.Security;

public static class Permissions
{
    public const string All = "*";

    public static class Dashboard
    {
        public const string View = "dashboard.view";
    }

    public static class Events
    {
        public const string All = "events.*";
        public const string View = "events.view";
    }

    public static class Contributions
    {
        public const string All = "contributions.*";
        public const string View = "contributions.view";
        public const string Create = "contributions.create";
    }

    public static class Donors
    {
        public const string All = "donors.*";
        public const string View = "donors.view";
    }

    public static class Reports
    {
        public const string View = "reports.view";
    }

    public static class Settlements
    {
        public const string All = "settlements.*";
    }

    public static class Branches
    {
        public const string All = "branches.*";
        public const string View = "branches.view";
    }

    public static class Users
    {
        public const string All = "users.*";
        public const string View = "users.view";
    }

    public static class Organisations
    {
        public const string View = "organisations.view";
    }

    public static class Settings
    {
        public const string All = "settings.*";
    }

    public static class Notifications
    {
        public const string All = "notification-templates.*";
    }

    public static class Donations
    {
        public const string View = "donations.view";
    }

    public static class Payments
    {
        public const string All = "payments.*";
        public const string View = "payments.view";
    }
}
