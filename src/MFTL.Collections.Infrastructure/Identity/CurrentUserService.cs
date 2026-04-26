using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Infrastructure.Identity;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? UserId => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) 
                             ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub");
    public string? Email => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email)
                             ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("email");
    public ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
