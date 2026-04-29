using MFTL.Collections.Domain.Common;

namespace MFTL.Collections.Domain.Events;

public sealed class ContributionRecordedEvent(Guid contributionId, Guid tenantId, Guid branchId, Guid eventId, Guid fundId, decimal amount, string currency, string contributorName, string? contributorEmail, string? contributorPhone) : BaseDomainEvent, IOutboxEvent
{
    public Guid ContributionId { get; } = contributionId;
    public Guid EventId { get; } = eventId;
    public Guid FundId { get; } = fundId;
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;
    public string ContributorName { get; } = contributorName;
    public string? ContributorEmail { get; } = contributorEmail;
    public string? ContributorPhone { get; } = contributorPhone;

    public Guid AggregateId => ContributionId;
    public Guid TenantId { get; } = tenantId;
    public Guid BranchId { get; } = branchId;
}

public sealed class ReceiptIssuedEvent(Guid receiptId, Guid tenantId, Guid branchId, Guid contributionId, string receiptNumber, string contributorName, string? contributorEmail, string? contributorPhone, decimal amount, string currency, string eventTitle) : BaseDomainEvent, IOutboxEvent
{
    public Guid ReceiptId { get; } = receiptId;
    public Guid ContributionId { get; } = contributionId;
    public string ReceiptNumber { get; } = receiptNumber;
    public string ContributorName { get; } = contributorName;
    public string? ContributorEmail { get; } = contributorEmail;
    public string? ContributorPhone { get; } = contributorPhone;
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;
    public string EventTitle { get; } = eventTitle;

    public Guid AggregateId => ReceiptId;
    public Guid TenantId { get; } = tenantId;
    public Guid BranchId { get; } = branchId;
}

public sealed class ReceiptResendRequestedEvent(Guid receiptId, Guid tenantId, Guid branchId, Guid contributionId, string receiptNumber, string contributorName, string? contributorEmail, string? contributorPhone, decimal amount, string currency, string eventTitle) : BaseDomainEvent, IOutboxEvent
{
    public Guid ReceiptId { get; } = receiptId;
    public Guid ContributionId { get; } = contributionId;
    public string ReceiptNumber { get; } = receiptNumber;
    public string ContributorName { get; } = contributorName;
    public string? ContributorEmail { get; } = contributorEmail;
    public string? ContributorPhone { get; } = contributorPhone;
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;
    public string EventTitle { get; } = eventTitle;

    public Guid AggregateId => ReceiptId;
    public Guid TenantId { get; } = tenantId;
    public Guid BranchId { get; } = branchId;
}

public sealed class UserInvitedEvent(Guid userId, Guid tenantId, string email, string name, string role) : BaseDomainEvent, IOutboxEvent
{
    public Guid UserId { get; } = userId;
    public string Email { get; } = email;
    public string Name { get; } = name;
    public string Role { get; } = role;

    public Guid AggregateId => UserId;
    public Guid TenantId { get; } = tenantId;
    public Guid BranchId => Guid.Empty; // Platform/Tenant level
}

public sealed class CollectorAssignedEvent(Guid userId, Guid tenantId, Guid branchId, Guid eventId, string eventTitle, string collectorName, string? collectorEmail) : BaseDomainEvent, IOutboxEvent
{
    public Guid UserId { get; } = userId;
    public Guid EventId { get; } = eventId;
    public string EventTitle { get; } = eventTitle;
    public string CollectorName { get; } = collectorName;
    public string? CollectorEmail { get; } = collectorEmail;

    public Guid AggregateId => UserId;
    public Guid TenantId { get; } = tenantId;
    public Guid BranchId { get; } = branchId;
}

public sealed class CashDropSubmittedEvent(Guid cashDropId, Guid tenantId, Guid branchId, Guid collectorId, decimal amount, string currency, string collectorName) : BaseDomainEvent, IOutboxEvent
{
    public Guid CashDropId { get; } = cashDropId;
    public Guid CollectorId { get; } = collectorId;
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;
    public string CollectorName { get; } = collectorName;

    public Guid AggregateId => CashDropId;
    public Guid TenantId { get; } = tenantId;
    public Guid BranchId { get; } = branchId;
}

public sealed class CashDropApprovedEvent(Guid cashDropId, Guid tenantId, Guid branchId, Guid collectorId, decimal amount, string currency, string collectorName) : BaseDomainEvent, IOutboxEvent
{
    public Guid CashDropId { get; } = cashDropId;
    public Guid CollectorId { get; } = collectorId;
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;
    public string CollectorName { get; } = collectorName;

    public Guid AggregateId => CashDropId;
    public Guid TenantId { get; } = tenantId;
    public Guid BranchId { get; } = branchId;
}

public sealed class EodClosedEvent(Guid branchId, Guid tenantId, string branchName, decimal totalAmount, string currency) : BaseDomainEvent, IOutboxEvent
{
    public Guid BranchId { get; } = branchId;
    public string BranchName { get; } = branchName;
    public decimal TotalAmount { get; } = totalAmount;
    public string Currency { get; } = currency;

    public Guid AggregateId => BranchId;
    public Guid TenantId { get; } = tenantId;
    public Guid BranchIdValue => BranchId;
    Guid IOutboxEvent.BranchId => BranchId;
}

public sealed class PaymentFailedEvent(Guid contributionId, Guid tenantId, Guid branchId, string contributorName, string? contributorEmail, decimal amount, string currency, string reason) : BaseDomainEvent, IOutboxEvent
{
    public Guid ContributionId { get; } = contributionId;
    public string ContributorName { get; } = contributorName;
    public string? ContributorEmail { get; } = contributorEmail;
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;
    public string Reason { get; } = reason;

    public Guid AggregateId => ContributionId;
    public Guid TenantId { get; } = tenantId;
    public Guid BranchId { get; } = branchId;
}

public sealed class SettlementReadyEvent(Guid settlementId, Guid tenantId, Guid branchId, Guid collectorId, string collectorName, decimal amount, string currency) : BaseDomainEvent, IOutboxEvent
{
    public Guid SettlementId { get; } = settlementId;
    public Guid CollectorId { get; } = collectorId;
    public string CollectorName { get; } = collectorName;
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;

    public Guid AggregateId => SettlementId;
    public Guid TenantId { get; } = tenantId;
    public Guid BranchId { get; } = branchId;
}
