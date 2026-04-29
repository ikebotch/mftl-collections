using MFTL.Collections.Domain.Common;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Domain.Entities;

/// <summary>
/// Database-backed notification template supporting multi-channel (SMS/Email/Push/InApp),
/// per-tenant and per-branch overrides, with system defaults as fallback.
/// </summary>
public sealed class NotificationTemplate : BaseTenantEntity
{
    /// <summary>Branch-specific override. Null means tenant-level template.</summary>
    public Guid? BranchId { get; set; }

    /// <summary>Logical key used in code, e.g. "receipt.issued", "user.invited".</summary>
    public string TemplateKey { get; set; } = string.Empty;

    public NotificationChannel Channel { get; set; }

    /// <summary>Human-readable name for admin UI.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Email subject line. Null for SMS/Push channels.</summary>
    public string? Subject { get; set; }

    /// <summary>Template body with {{variableName}} placeholders.</summary>
    public string Body { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>System-wide default (TenantId = Guid.Empty). Not editable by tenants.</summary>
    public bool IsSystemDefault { get; set; }
}
