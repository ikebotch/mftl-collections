using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Features.Tenants.Queries.ListTenants;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Api.Extensions;

namespace MFTL.Collections.Api.Functions.Tenants;

public class TenantFunctions(
    IMediator mediator)
{
    [Function("ListTenants")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Tenants.Base)] HttpRequest req)
    {
        // ListTenants is a context-bootstrap endpoint — it must NOT require X-Tenant-Id
        // or a permission guard. The handler filters results to the user's AllowedTenantIds.
        // Platform Admins see all tenants.
        if (req.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return new ObjectResult(new ApiResponse(false, "Authentication required."))
                { StatusCode = StatusCodes.Status401Unauthorized };
        }

        var result = await mediator.Send(new ListTenantsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<TenantDto>>(true, "Tenants retrieved.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
