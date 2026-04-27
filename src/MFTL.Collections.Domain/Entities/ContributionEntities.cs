using MFTL.Collections.Domain.Common;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty; // subdomain or slug
    public string? SupportEmail { get; set; }
    public string? MissionStatement { get; set; }
    public string DefaultCurrency { get; set; } = "GHS";
    public string DefaultLocale { get; set; } = "en-GH";
    public string? PrimaryLogoUrl { get; set; }
    public string? PosLogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    
    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}

public sealed class Branch : BaseTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Event : BaseBranchEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset? EventDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? DisplayImageUrl { get; set; }
    public string? ReceiptLogoUrl { get; set; }
    public string? Metadata { get; set; }
    public Branch? Branch { get; set; }

    public ICollection<RecipientFund> RecipientFunds { get; set; } = new List<RecipientFund>();
    public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
}

public sealed class RecipientFund : BaseBranchEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CollectedAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Metadata { get; set; }
    public Branch? Branch { get; set; }
    public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
}

public sealed class Contributor : BaseBranchEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsAnonymous { get; set; }
    public Branch? Branch { get; set; }
}

public sealed class Contribution : BaseBranchEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid RecipientFundId { get; set; }
    public RecipientFund RecipientFund { get; set; } = null!;
    public Guid? ContributorId { get; set; }
    public Contributor? Contributor { get; set; }
    
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GHS";
    public string ContributorName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty; // Cash, Card, MoMo
    public ContributionStatus Status { get; set; }
    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public string? Note { get; set; }
    public string? Reference { get; set; }
    public Branch? Branch { get; set; }
    public Receipt? Receipt { get; set; }
}

public sealed class Receipt : BaseBranchEntity
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid RecipientFundId { get; set; }
    public RecipientFund RecipientFund { get; set; } = null!;
    public Guid ContributionId { get; set; }
    public Contribution Contribution { get; set; } = null!;
    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public User? RecordedByUser { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public ReceiptStatus Status { get; set; } = ReceiptStatus.Issued;
    public string? Note { get; set; }
    public string? Metadata { get; set; }
    public Branch? Branch { get; set; }
}

public sealed class Settlement : BaseBranchEntity
{
    public Guid CollectorId { get; set; }
    public User Collector { get; set; } = null!;
    
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GHS";
    public DateTimeOffset SettlementDate { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "Pending"; // Pending, Reviewed, Completed
    public string? Note { get; set; }
    public Branch? Branch { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
}
