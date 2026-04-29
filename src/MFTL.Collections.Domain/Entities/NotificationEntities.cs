using MFTL.Collections.Domain.Common;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Domain.Entities;

public sealed class OutboxMessage : BaseBranchEntity
{
    public string EventType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? CorrelationId { get; set; }
    public int Priority { get; set; } = 0; // 0 = Default, Higher is faster
}

public sealed class Notification : BaseBranchEntity
{
    public Guid? OutboxMessageId { get; set; }
    public OutboxMessage? OutboxMessage { get; set; }
    public string? RecipientUserId { get; set; }
    public string? RecipientEmail { get; set; }
    public string? RecipientPhone { get; set; }
    public string? RecipientType { get; set; } // Donor, Collector, Admin, Supervisor, etc.
    public NotificationChannel Channel { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public string? ProviderMessageId { get; set; }
    public string? ProviderResponse { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class NotificationPreference : BaseBranchEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public bool IsEnabled { get; set; } = true;
}
