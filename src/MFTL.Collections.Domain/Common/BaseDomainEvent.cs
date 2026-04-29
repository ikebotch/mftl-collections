namespace MFTL.Collections.Domain.Common;

public abstract class BaseDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
