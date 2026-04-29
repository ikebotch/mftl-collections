using MFTL.Collections.Domain.Common;

namespace MFTL.Collections.Domain.Entities;

public sealed class NotificationTemplate : BaseTenantEntity
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
