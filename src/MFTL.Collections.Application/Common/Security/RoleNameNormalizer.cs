namespace MFTL.Collections.Application.Common.Security;

public static class RoleNameNormalizer
{
    public static string Normalize(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return string.Empty;

        return roleName.Trim() switch
        {
            "TenantAdmin" => "Tenant Admin",
            "OrganisationAdmin" => "Organisation Admin",
            "OrganizationAdmin" => "Organisation Admin",
            "PlatformAdmin" => "Platform Admin",
            "FinanceAdmin" => "Finance Admin",
            "BranchAdmin" => "Branch Admin",
            "EventManager" => "Event Manager",
            "NotificationManager" => "Notification Manager",
            
            _ => roleName
        };
    }
}
