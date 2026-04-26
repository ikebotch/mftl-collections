using MFTL.Collections.Domain.Common;

namespace MFTL.Collections.Domain.Entities;

public sealed class User : BaseEntity
{
    public string Auth0Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsPlatformAdmin { get; set; }
    public bool IsSuspended { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public UserInviteStatus InviteStatus { get; set; } = UserInviteStatus.Accepted;

    public ICollection<UserScopeAssignment> ScopeAssignments { get; set; } = new List<UserScopeAssignment>();
    public ICollection<Receipt> RecordedReceipts { get; set; } = new List<Receipt>();
}

public sealed class UserScopeAssignment : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    
    public ScopeType ScopeType { get; set; }
    public Guid? TargetId { get; set; } // TenantId, EventId, or RecipientFundId
    
    public string Role { get; set; } = string.Empty; // e.g. PlatformAdmin, TenantAdmin, FinanceAdmin, EventManager, Collector, Viewer
    
    public Guid? CollectorId { get; set; } // Link to collector profile if role is Collector
}

public enum ScopeType
{
    Platform,
    Organisation, // Alias for Tenant
    Branch,
    Event,
    RecipientFund,
    CollectorSelf
}

public enum UserInviteStatus
{
    Pending,
    Accepted,
    Expired,
    Cancelled
}
