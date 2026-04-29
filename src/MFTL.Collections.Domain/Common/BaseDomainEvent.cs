namespace MFTL.Collections.Domain.Common;

public abstract class BaseDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}

public interface IOutboxEvent
{
    Guid AggregateId { get; }
    Guid TenantId { get; }
    Guid BranchId { get; }
}
