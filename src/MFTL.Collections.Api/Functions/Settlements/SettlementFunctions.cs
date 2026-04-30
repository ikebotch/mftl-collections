using MediatR;
using MFTL.Collections.Application.Features.Settlements.Queries.ListSettlements;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace MFTL.Collections.Api.Functions.Settlements;

public class SettlementFunctions(
    IMediator mediator,
    IScopeAccessService scopeService,
    ITenantContext tenantContext)
{
    [Function("ListSettlements")]
    public async Task<IActionResult> ListSettlements(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Settlements.Base)] HttpRequest req)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Settlements.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new ListSettlementsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<SettlementDto>>(true, "Settlements retrieved.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
