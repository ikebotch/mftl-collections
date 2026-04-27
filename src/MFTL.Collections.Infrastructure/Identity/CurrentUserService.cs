using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Infrastructure.Identity;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : ICurrentUserService
{
    private const string RoleClaim = "https://mftl.com/roles";
    private readonly bool _bypassAuth = configuration.GetValue<bool>("Values:DEV_AUTH_BYPASS") || configuration.GetValue<bool>("DEV_AUTH_BYPASS");

    public string? UserId => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) 
                             ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub")
                             ?? (_bypassAuth ? "dev-auth0-id" : null);

    public string? Email => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email)
                             ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("email")
                             ?? (_bypassAuth ? "dev-admin@mftl.local" : null);

    public ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;
    
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
