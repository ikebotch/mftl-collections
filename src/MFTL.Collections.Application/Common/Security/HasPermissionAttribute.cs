namespace MFTL.Collections.Application.Common.Security;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class HasPermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}
