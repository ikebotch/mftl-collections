using MFTL.Collections.Domain.Common;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Domain.Entities;

public sealed class CashDrop : BaseBranchEntity
{
    public Guid CollectorId { get; set; }
    public User Collector { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GHS";
    public CashDropStatus Status { get; set; } = CashDropStatus.Submitted;
    public string? Note { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public sealed class EodReport : BaseBranchEntity
{
    public string Title { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "GHS";
    public DateTimeOffset ClosedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ClosedByUserId { get; set; }
    public User ClosedByUser { get; set; } = null!;
    public string? Metadata { get; set; }
}

public enum CashDropStatus
{
    Submitted,
    Approved,
    Rejected
}
