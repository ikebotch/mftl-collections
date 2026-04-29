namespace MFTL.Collections.Contracts.Common;

public static class ApiRoutes
{
    // Removing "api" root prefix as Azure Functions host adds it by default
    public const string Version = "v1";
    public const string BasePath = Version;

    public static class Events
    {
        public const string Base = BasePath + "/events";
        public const string GetById = Base + "/{id}";
        public const string Update = Base + "/{id}";
        public const string AssignStaff = Base + "/{id}/staff";
    }

    public static class RecipientFunds
    {
        public const string Base = BasePath + "/recipient-funds";
        public const string GetById = Base + "/{id}";
        public const string ListByEvent = Base + "/event/{eventId}";
        public const string Update = Base + "/{id}";
    }

    public static class Contributions
    {
        public const string Base = BasePath + "/contributions";
        public const string GetById = Base + "/{id}";
        public const string RecordCash = Base + "/cash";
        public const string Update = Base + "/{id}";
    }

    public static class Donors
    {
        public const string Base = BasePath + "/donors";
        public const string GetById = Base + "/{id}";
    }

    public static class Collectors
    {
        public const string AdminBase = BasePath + "/collectors";
        public const string GetById = AdminBase + "/{id}";
        public const string Update = AdminBase + "/{id}";
        
        public const string MobileBase = BasePath + "/collector";
        public const string Me = MobileBase + "/me";
        public const string Assignments = MobileBase + "/assignments";
        public const string History = MobileBase + "/history";
    }

    public static class Payments
    {
        public const string Base = BasePath + "/payments";
        public const string Initiate = Base + "/initiate";
        public const string GetById = Base + "/{id}";
        public const string Webhook = "v1/webhooks/payments";
    }

    public static class Receipts
    {
        public const string Base = BasePath + "/receipts";
        public const string GetById = Base + "/{id}";
    }

    public static class Dashboards
    {
        public const string Base = BasePath + "/dashboards";
        public const string Admin = Base + "/admin";
        public const string Event = Base + "/events/{id}";
        public const string Recipient = Base + "/recipient-funds/{id}";
    }

    public static class Users
    {
        public const string Base = BasePath + "/users";
        public const string GetById = Base + "/{id}";
        public const string Update = Base + "/{id}";
        public const string Invite = Base + "/invite";
        public const string UpdateStatus = Base + "/{id}/status";
        public const string Audit = Base + "/{id}/audit";
    }

    public static class Settlements
    {
        public const string Base = BasePath + "/settlements";
        public const string GetById = Base + "/{id}";
    }

    public static class Branches
    {
        public const string Base = BasePath + "/branches";
        public const string GetById = Base + "/{id}";
        public const string Update = Base + "/{id}";
    }

    public static class Tenants
    {
        public const string Base = BasePath + "/tenants";
    }

    public static class NotificationTemplates
    {
        public const string Base = BasePath + "/notification-templates";
        public const string GetById = Base + "/{id}";
        public const string Preview = Base + "/{id}/preview";
        public const string SendTest = Base + "/{id}/send-test";
    }

    public static class Storefront
    {
        public const string Base = BasePath + "/storefront";
        public const string GetEventBySlug = Base + "/events/{slug}";
        public const string ListFundsByEventSlug = Base + "/events/{slug}/funds";
    }
}
