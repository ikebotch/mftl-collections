using MFTL.Collections.Domain.Common;

namespace MFTL.Collections.Domain.Entities;

public sealed class User : BaseEntity
{
    public string Auth0Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsPlatformAdmin { get; set; }

    public ICollection<UserScopeAssignment> ScopeAssignments { get; set; } = new List<UserScopeAssignment>();
}

public sealed class UserScopeAssignment : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public ScopeType ScopeType { get; set; }
    public Guid? TargetId { get; set; } // TenantId, EventId, or RecipientFundId
    
    public string Role { get; set; } = string.Empty; // e.g. Admin, Viewer, Collector
}

public enum ScopeType
{
    Platform,
    Tenant,
    Event,
    RecipientFund
}
