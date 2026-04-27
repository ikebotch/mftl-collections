using System.Threading;

namespace MFTL.Collections.Infrastructure.Tenancy;

public static class TenancyContextStore
{
    private static readonly AsyncLocal<Guid[]> _tenantIds = new();
    private static readonly AsyncLocal<Guid[]> _branchIds = new();
    private static readonly AsyncLocal<bool> _isPlatform = new();
    private static readonly AsyncLocal<bool> _isGlobalBranch = new();

    public static Guid[] CurrentTenantIds
    {
        get => _tenantIds.Value ?? Array.Empty<Guid>();
        set => _tenantIds.Value = value;
    }

    public static Guid[] CurrentBranchIds
    {
        get => _branchIds.Value ?? Array.Empty<Guid>();
        set => _branchIds.Value = value;
    }

    public static bool IsPlatform
    {
        get => _isPlatform.Value;
        set => _isPlatform.Value = value;
    }

    public static bool IsGlobalBranch
    {
        get => _isGlobalBranch.Value;
        set => _isGlobalBranch.Value = value;
    }
}
