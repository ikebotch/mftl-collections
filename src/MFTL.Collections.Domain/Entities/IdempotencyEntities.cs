using MFTL.Collections.Domain.Common;

namespace MFTL.Collections.Domain.Entities;

public sealed class ProcessedWebhookEvent : BaseEntity
{
    public string Provider { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty; // External provider event ID
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
