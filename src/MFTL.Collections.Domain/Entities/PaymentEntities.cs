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
    public string? ProviderPayload { get; set; } // stored as jsonb in DB
    public DateTimeOffset? ProcessedAt { get; set; }
    public Receipt? Receipt { get; set; }
}
