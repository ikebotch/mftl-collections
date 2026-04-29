using System.Collections.Immutable;

namespace MFTL.Collections.Api.Middleware;

public static class EndpointAccessPolicies
{
    public static readonly ImmutableDictionary<string, EndpointAccessPolicy> Registry = new Dictionary<string, EndpointAccessPolicy>
    {
        // Public Endpoints
        { "ScalarUi", new(EndpointAccessPolicyType.Public) },
        { "SwaggerJson", new(EndpointAccessPolicyType.Public) },
        { "Storefront_GetEventBySlug", new(EndpointAccessPolicyType.Public) },
        { "Storefront_ListFundsByEventSlug", new(EndpointAccessPolicyType.Public) },

        // Authenticated Endpoints (General access)
        { "GetMe", new(EndpointAccessPolicyType.Authenticated) },
        { "GetCollectorMe", new(EndpointAccessPolicyType.Authenticated) },
        { "GetCollectorAssignments", new(EndpointAccessPolicyType.Authenticated) },

        // Permission-based Endpoints
        { "ListTenants", new(EndpointAccessPolicyType.Permission, "organisations.view") },
        { "CreateTenant", new(EndpointAccessPolicyType.Permission, "organisations.create") },
        { "UpdateTenant", new(EndpointAccessPolicyType.Permission, "organisations.update") },

        { "ListBranches", new(EndpointAccessPolicyType.Permission, "branches.view") },
        { "GetBranch", new(EndpointAccessPolicyType.Permission, "branches.view") },
        { "CreateBranch", new(EndpointAccessPolicyType.Permission, "branches.create") },
        { "UpdateBranch", new(EndpointAccessPolicyType.Permission, "branches.update") },
        { "DeactivateBranch", new(EndpointAccessPolicyType.Permission, "branches.deactivate") },
        { "DeleteBranch", new(EndpointAccessPolicyType.Permission, "branches.delete") },

        { "ListEvents", new(EndpointAccessPolicyType.Permission, "events.view") },
        { "GetEventById", new(EndpointAccessPolicyType.Permission, "events.view") },
        { "CreateEvent", new(EndpointAccessPolicyType.Permission, "events.create") },
        { "UpdateEvent", new(EndpointAccessPolicyType.Permission, "events.update") },
        { "AssignStaffToEvent", new(EndpointAccessPolicyType.Permission, "events.manage") },

        { "ListRecipientFunds", new(EndpointAccessPolicyType.Permission, "funds.view") },
        { "ListRecipientFundsByEvent", new(EndpointAccessPolicyType.Permission, "funds.view") },
        { "GetRecipientFundById", new(EndpointAccessPolicyType.Permission, "funds.view") },
        { "CreateRecipientFund", new(EndpointAccessPolicyType.Permission, "funds.create") },
        { "UpdateRecipientFund", new(EndpointAccessPolicyType.Permission, "funds.update") },

        { "ListCollectors", new(EndpointAccessPolicyType.Permission, "collectors.view") },
        { "GetCollectorById", new(EndpointAccessPolicyType.Permission, "collectors.view") },
        { "GetCollectorHistory", new(EndpointAccessPolicyType.Authenticated) },
        { "CreateCollector", new(EndpointAccessPolicyType.Permission, "collectors.create") },
        { "UpdateCollector", new(EndpointAccessPolicyType.Permission, "collectors.update") },
        { "SetCollectorPin", new(EndpointAccessPolicyType.Authenticated) },

        { "RecordCashContribution", new(EndpointAccessPolicyType.Permission, "contributions.create") },
        { "ListContributions", new(EndpointAccessPolicyType.Permission, "contributions.view") },
        { "GetContributionById", new(EndpointAccessPolicyType.Permission, "contributions.view") },
        { "UpdateContribution", new(EndpointAccessPolicyType.Permission, "contributions.update") },

        { "ListReceipts", new(EndpointAccessPolicyType.Permission, "receipts.view") },
        { "GetReceiptById", new(EndpointAccessPolicyType.Permission, "receipts.view") },
        { "ResendReceipt", new(EndpointAccessPolicyType.Authenticated) },

        { "InitiateContributionPayment", new(EndpointAccessPolicyType.Permission, "payments.create") },
        { "ListPayments", new(EndpointAccessPolicyType.Permission, "payments.view") },
        { "GetPaymentById", new(EndpointAccessPolicyType.Permission, "payments.view") },

        { "ListSettlements", new(EndpointAccessPolicyType.Permission, "settlements.view") },
        
        { "ListSmsTemplates", new(EndpointAccessPolicyType.Permission, "sms_templates.read") },
        { "GetSmsTemplateById", new(EndpointAccessPolicyType.Permission, "sms_templates.read") },
        { "CreateSmsTemplate", new(EndpointAccessPolicyType.Permission, "sms_templates.create") },
        { "UpdateSmsTemplate", new(EndpointAccessPolicyType.Permission, "sms_templates.update") },

        { "ListDonors", new(EndpointAccessPolicyType.Permission, "donors.view") },
        { "GetDonorById", new(EndpointAccessPolicyType.Permission, "donors.view") },

        { "GetAdminDashboard", new(EndpointAccessPolicyType.Permission, "reports.admin") },
        { "GetEventDashboard", new(EndpointAccessPolicyType.Permission, "reports.event") },
        { "GetRecipientDashboard", new(EndpointAccessPolicyType.Permission, "reports.recipient") },

        { "CoreListUsers", new(EndpointAccessPolicyType.Permission, "users.view") },
        { "GetUserById", new(EndpointAccessPolicyType.Permission, "users.view") },
        { "InviteUser", new(EndpointAccessPolicyType.Permission, "users.invite") },
        { "UpdateUser", new(EndpointAccessPolicyType.Permission, "users.update") },
        { "UpdateUserStatus", new(EndpointAccessPolicyType.Permission, "users.status") },
        { "GetUserAuditLogs", new(EndpointAccessPolicyType.Permission, "audit.view") },
        { "AssignUserScope", new(EndpointAccessPolicyType.Permission, "scopes.manage") },
        { "RevokeUserScope", new(EndpointAccessPolicyType.Permission, "scopes.manage") },

        // Webhooks
        { "Auth0UserCreatedWebhook", new(EndpointAccessPolicyType.WebhookSecret, SecretName: "AUTH0_WEBHOOK_SECRET") },
        { "PaymentWebhook", new(EndpointAccessPolicyType.WebhookSecret, SecretName: "PAYMENT_WEBHOOK_SECRET") },

        // Platform Only
        { "Auth0Provisioning", new(EndpointAccessPolicyType.PlatformOnly) },
    }.ToImmutableDictionary();
}
