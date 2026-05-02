using MFTL.Collections.Domain.Common;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Domain.Entities;

public sealed class Payment : BaseTenantEntity
{
    public Guid ContributionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GHS";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string Method { get; set; } = "Cash"; // Cash, MobileMoney, Card
    public string? ProviderReference { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? ProviderPayload { get; set; } // stored as jsonb in DB
    public DateTimeOffset? ProcessedAt { get; set; }
    public Receipt? Receipt { get; set; }
}

public sealed class ProcessedExternalPaymentCallback : BaseEntity
{
    public string CallbackEventId { get; set; } = string.Empty;
    public string PaymentServicePaymentId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public Guid ContributionId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderReference { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public string ExternalReference { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public DateTimeOffset? OccurredAt { get; set; }
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
