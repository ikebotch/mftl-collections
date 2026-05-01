namespace MFTL.Collections.Application.Common.Security;

public static class RoleNameNormalizer
{
    /// <summary>
    /// Strictly enforces canonical role keys. 
    /// No longer supports legacy aliases like "Organization Admin" or "Tenant Admin".
    /// </summary>
    public static string Normalize(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return string.Empty;

        var trimmed = roleName.Trim();
        
        // Fast-path: if it's already a valid canonical key, return it.
        if (AppRoles.IsValid(trimmed))
        {
            return trimmed;
        }

        // We no longer support silent aliases. 
        // In dev/test, this will help identify where old strings are still being used.
        return trimmed;
    }
}
