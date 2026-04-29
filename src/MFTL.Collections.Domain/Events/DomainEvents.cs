using MFTL.Collections.Domain.Common;

namespace MFTL.Collections.Domain.Events;

public sealed class ContributionRecordedEvent(Guid contributionId, Guid eventId, Guid fundId, decimal amount, string currency, string contributorName, string? contributorEmail, string? contributorPhone) : BaseDomainEvent
{
    public Guid ContributionId { get; } = contributionId;
    public Guid EventId { get; } = eventId;
    public Guid FundId { get; } = fundId;
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;
    public string ContributorName { get; } = contributorName;
    public string? ContributorEmail { get; } = contributorEmail;
    public string? ContributorPhone { get; } = contributorPhone;
}

public sealed class ReceiptIssuedEvent(Guid receiptId, Guid contributionId, string receiptNumber, string contributorName, string? contributorEmail, string? contributorPhone, decimal amount, string currency, string eventTitle) : BaseDomainEvent
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
}

public sealed class UserInvitedEvent(Guid userId, string email, string name, string role) : BaseDomainEvent
{
    public Guid UserId { get; } = userId;
    public string Email { get; } = email;
    public string Name { get; } = name;
    public string Role { get; } = role;
}

public sealed class CollectorAssignedEvent(Guid userId, Guid eventId, string eventTitle, string collectorName, string? collectorEmail) : BaseDomainEvent
{
    public Guid UserId { get; } = userId;
    public Guid EventId { get; } = eventId;
    public string EventTitle { get; } = eventTitle;
    public string CollectorName { get; } = collectorName;
    public string? CollectorEmail { get; } = collectorEmail;
}

public sealed class CashDropSubmittedEvent(Guid cashDropId, Guid collectorId, decimal amount, string currency, string collectorName) : BaseDomainEvent
{
    public Guid CashDropId { get; } = cashDropId;
    public Guid CollectorId { get; } = collectorId;
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;
    public string CollectorName { get; } = collectorName;
}

public sealed class CashDropApprovedEvent(Guid cashDropId, Guid collectorId, decimal amount, string currency, string collectorName) : BaseDomainEvent
{
    public Guid CashDropId { get; } = cashDropId;
    public Guid CollectorId { get; } = collectorId;
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;
    public string CollectorName { get; } = collectorName;
}

public sealed class EodClosedEvent(Guid branchId, string branchName, decimal totalAmount, string currency) : BaseDomainEvent
{
    public Guid BranchId { get; } = branchId;
    public string BranchName { get; } = branchName;
    public decimal TotalAmount { get; } = totalAmount;
    public string Currency { get; } = currency;
}

public sealed class PaymentFailedEvent(Guid contributionId, string contributorName, string? contributorEmail, decimal amount, string currency, string reason) : BaseDomainEvent
{
    public Guid ContributionId { get; } = contributionId;
    public string ContributorName { get; } = contributorName;
    public string? ContributorEmail { get; } = contributorEmail;
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;
    public string Reason { get; } = reason;
}

public sealed class SettlementReadyEvent(Guid settlementId, Guid collectorId, string collectorName, decimal amount, string currency) : BaseDomainEvent
{
    public Guid SettlementId { get; } = settlementId;
    public Guid CollectorId { get; } = collectorId;
    public string CollectorName { get; } = collectorName;
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;
}
