namespace MFTL.Collections.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    private readonly List<BaseDomainEvent> _domainEvents = new();
    public IReadOnlyList<BaseDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(BaseDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void RemoveDomainEvent(BaseDomainEvent domainEvent) => _domainEvents.Remove(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public abstract class BaseTenantEntity : BaseEntity
{
    public Guid TenantId { get; set; }
}

public abstract class BaseBranchEntity : BaseTenantEntity
{
    public Guid BranchId { get; set; }
}
