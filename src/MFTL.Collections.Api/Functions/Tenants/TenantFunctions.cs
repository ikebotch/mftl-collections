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
    IMediator mediator,
    IScopeAccessService scopeService,
    ITenantContext tenantContext)
{
    [Function("ListTenants")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Tenants.Base)] HttpRequest req)
    {
        // For the base list tenants endpoint, we check if they have organisations.view.
        // If they are a platform admin, they'll see all. If they are a tenant user,
        // they'll see only their assigned tenants (enforced in the query handler).
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Organisations.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new ListTenantsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<TenantDto>>(true, "Tenants retrieved.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
