using MFTL.Collections.Domain.Common;

namespace MFTL.Collections.Domain.Entities;

public sealed class Permission : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
}

public sealed class RolePermission : BaseEntity
{
    public string RoleName { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
}
