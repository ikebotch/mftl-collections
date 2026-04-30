using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Tenancy;

namespace MFTL.Collections.Infrastructure.Identity;

public sealed class CurrentUserService(
    IHttpContextAccessor httpContextAccessor,
    FunctionHttpRequestAccessor requestAccessor) : ICurrentUserService
{
    private const string DevUserIdHeader = "X-Dev-User-Id";

    public string? UserId
    {
        get
        {
            var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // In Azure Functions Isolated, IHttpContextAccessor might not have the user set by middleware
            if (string.IsNullOrEmpty(userId))
            {
                userId = httpContextAccessor.HttpContext?.Items["UserId"] as string;
            }

            if (string.IsNullOrEmpty(userId))
            {
                userId = requestAccessor.UserId;
            }

            if (string.IsNullOrEmpty(userId))
            {
                userId = requestAccessor.Headers[DevUserIdHeader].FirstOrDefault();
            }

            return userId;
        }
    }

    public string? Email => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);
    public ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;
    public bool IsAuthenticated => 
        User?.Identity?.IsAuthenticated == true || 
        !string.IsNullOrEmpty(UserId);
}
