using MFTL.Collections.Domain.Common;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Domain.Entities;

public sealed class NotificationTemplate : BaseTenantEntity, IOptionalBranchEntity
{
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    
    public string TemplateKey { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty; // Sms, Email, Push, InApp, WhatsApp
    public string Name { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystemDefault { get; set; }
}

public sealed class Notification : BaseTenantEntity, IOptionalBranchEntity
{
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public Guid? OutboxMessageId { get; set; }
    public Guid? ReceiptId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? ContributionId { get; set; }

    public NotificationChannel Channel { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public string TemplateKey { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string? RecipientPhone { get; set; }
    public string? RecipientEmail { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? ProviderMessageId { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}

public sealed class OutboxMessage : BaseTenantEntity, IOptionalBranchEntity
{
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public Guid AggregateId { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public string CorrelationId { get; set; } = string.Empty;
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public int AttemptCount { get; set; }
    public int Priority { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? LastError { get; set; }
}
