namespace MFTL.Collections.Application.Common.Security;

public interface IAccessPolicyResolver
{
    Task<IAccessPolicy> ResolvePolicyAsync();
    Task<AccessContext> GetAccessContextAsync();
}
