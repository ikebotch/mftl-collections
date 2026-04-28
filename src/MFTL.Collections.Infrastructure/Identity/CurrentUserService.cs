using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Infrastructure.Identity;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : ICurrentUserService
{
    private const string RoleClaim = "https://mftl.com/roles";
    private ClaimsPrincipal? _user;
    private readonly bool _bypassAuth = configuration.GetValue<bool>("Values:DEV_AUTH_BYPASS") || configuration.GetValue<bool>("DEV_AUTH_BYPASS");

    public void SetUser(ClaimsPrincipal user) => _user = user;

    public string? UserId => (_user ?? httpContextAccessor.HttpContext?.User)?.FindFirstValue(ClaimTypes.NameIdentifier) 
                             ?? (_user ?? httpContextAccessor.HttpContext?.User)?.FindFirstValue("sub")
                             ?? (_bypassAuth ? "dev-auth0-id" : null);

    public string? Email => (_user ?? httpContextAccessor.HttpContext?.User)?.FindFirstValue(ClaimTypes.Email)
                             ?? (_user ?? httpContextAccessor.HttpContext?.User)?.FindFirstValue("email")
                             ?? (_user ?? httpContextAccessor.HttpContext?.User)?.FindFirstValue("https://mftl.com/email")
                             ?? (_bypassAuth ? "dev-admin@mftl.local" : null);

    public string? Name => (_user ?? httpContextAccessor.HttpContext?.User)?.FindFirstValue(ClaimTypes.Name)
                             ?? (_user ?? httpContextAccessor.HttpContext?.User)?.FindFirstValue("name")
                             ?? (_user ?? httpContextAccessor.HttpContext?.User)?.FindFirstValue("nickname")
                             ?? (_user ?? httpContextAccessor.HttpContext?.User)?.FindFirstValue("preferred_username")
                             ?? (_bypassAuth ? "Development Admin" : null);

    public ClaimsPrincipal? User => _user ?? httpContextAccessor.HttpContext?.User;
    
    public bool IsAuthenticated => (User?.Identity?.IsAuthenticated ?? false) || _bypassAuth;

    public bool IsPlatformAdmin => 
        _bypassAuth || 
        (Roles.Any(r => 
            string.Equals(r, "Platform Admin", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(r, "super_admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "platform_admin", StringComparison.OrdinalIgnoreCase)));

    public IEnumerable<string> Roles => _bypassAuth 
        ? new[] { "Platform Admin" } 
        : (User?.Claims
            .Where(c => c.Type == RoleClaim || c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .Distinct() ?? Enumerable.Empty<string>());
}
