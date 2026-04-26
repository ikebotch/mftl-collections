using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Features.Tenants.Queries.ListTenants;

namespace MFTL.Collections.Api.Functions.Tenants;

public class TenantFunctions(IMediator mediator)
{
    [Function("ListTenants")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Tenants.Base)] HttpRequest req)
    {
        var result = await mediator.Send(new ListTenantsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<TenantDto>>(true, "Tenants retrieved.", result));
    }
}
