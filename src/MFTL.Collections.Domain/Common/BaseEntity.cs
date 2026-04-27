namespace MFTL.Collections.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
}

public abstract class BaseTenantEntity : BaseEntity
{
    public Guid TenantId { get; set; }
}

public abstract class BaseBranchEntity : BaseEntity
{
    public Guid BranchId { get; set; }
}
