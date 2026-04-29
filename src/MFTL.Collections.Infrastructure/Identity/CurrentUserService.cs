using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Infrastructure.Identity;

public sealed class CurrentUserService : ICurrentUserService
{
    private const string RoleClaim = "https://mftl.com/roles";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly bool _bypassAuth;
    private ClaimsPrincipal? _user;

    private string? _accessToken;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        
        var bypassVal = _configuration["Values:DEV_AUTH_BYPASS"] ?? _configuration["DEV_AUTH_BYPASS"];
        _bypassAuth = string.Equals(bypassVal, "true", StringComparison.OrdinalIgnoreCase);
        
        // Use console log to be visible in func start
        System.Console.WriteLine($"[CurrentUserService] DEV_AUTH_BYPASS raw: '{bypassVal}', parsed: {_bypassAuth}");
    }

    public void SetUser(ClaimsPrincipal user) => _user = user;
    public void SetToken(string token) => _accessToken = token;

    public string? UserId => (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue(ClaimTypes.NameIdentifier) 
                             ?? (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue("sub")
                             ?? (_bypassAuth ? "dev-auth0-id" : null);

    public string? Email => (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue(ClaimTypes.Email)
                             ?? (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue("email")
                             ?? (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue("https://mftl.com/email")
                             ?? (_bypassAuth ? "dev-admin@mftl.local" : null);

    public string? Name => (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue(ClaimTypes.Name)
                             ?? (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue("name")
                             ?? (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue("nickname")
                             ?? (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue("preferred_username")
                             ?? (_bypassAuth ? "Development Admin" : null);

    public string? GivenName => (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue(ClaimTypes.GivenName)
                                 ?? (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue("given_name");

    public string? FamilyName => (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue(ClaimTypes.Surname)
                                 ?? (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue("family_name");

    public string? Nickname => (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue("nickname");

    public string? Picture => (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue("picture");
    
    public string? PhoneNumber => (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue("phone_number")
                                  ?? (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue(ClaimTypes.MobilePhone)
                                  ?? (_user ?? _httpContextAccessor.HttpContext?.User)?.FindFirstValue(ClaimTypes.HomePhone);

    public string? AccessToken 
    {
        get
        {
            if (!string.IsNullOrEmpty(_accessToken)) return _accessToken;

            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return authHeader.Substring("Bearer ".Length).Trim();
        }
    }

    public ClaimsPrincipal? User => _user ?? _httpContextAccessor.HttpContext?.User;
    
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
