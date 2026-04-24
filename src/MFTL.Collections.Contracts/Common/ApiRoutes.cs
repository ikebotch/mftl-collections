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
    }

    public static class RecipientFunds
    {
        public const string Base = BasePath + "/recipient-funds";
        public const string ListByEvent = Base + "/event/{eventId}";
    }

    public static class Contributions
    {
        public const string Base = BasePath + "/contributions";
        public const string GetById = Base + "/{id}";
        public const string RecordCash = Base + "/cash";
    }

    public static class Collectors
    {
        public const string Base = BasePath + "/collector";
        public const string Me = Base + "/me";
        public const string Assignments = Base + "/assignments";
        public const string History = Base + "/history";
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
        public const string Event = Base + "/events/{id}";
        public const string Recipient = Base + "/recipient-funds/{id}";
    }
}
