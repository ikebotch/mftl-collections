using MFTL.Collections.Domain.Common;

namespace MFTL.Collections.Domain.Entities;

public sealed class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty; // e.g. UserInvited, RoleChanged
    public string EntityName { get; set; } = string.Empty; // e.g. User, Event
    public string EntityId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty; // JSON or descriptive text
    public string PerformedBy { get; set; } = string.Empty; // User Name or ID
    public Guid? TenantId { get; set; }
}
