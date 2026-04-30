using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Features.Donors.Queries.GetDonorById;
using MFTL.Collections.Application.Features.Donors.Queries.ListDonors;

namespace MFTL.Collections.Api.Functions.Donors;

public class DonorFunctions(
    IMediator mediator,
    IScopeAccessService scopeService,
    ITenantContext tenantContext)
{
    [Function("ListDonors")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Donors.Base)] HttpRequest req)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Donors.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new ListDonorsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<DonorDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetDonorById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Donors.GetById)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Donors.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new GetDonorByIdQuery(id));
        if (result == null) return new NotFoundResult();
        return new OkObjectResult(new ApiResponse<DonorDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
