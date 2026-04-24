using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorAssignments;
using MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorMe;
using MFTL.Collections.Application.Features.Collectors.Queries.ListCollectorHistory;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Api.Functions.Collectors;

public class CollectorFunctions(IMediator mediator)
{
    private const string DevUserIdHeader = "X-Dev-User-Id";

    [Function("GetCollectorMe")]
    public async Task<IActionResult> GetMe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.Me)] HttpRequest req)
    {
        var result = await mediator.Send(new GetCollectorMeQuery(req.Headers[DevUserIdHeader].FirstOrDefault()));
        return new OkObjectResult(new ApiResponse<CollectorMeDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetCollectorAssignments")]
    public async Task<IActionResult> GetAssignments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.Assignments)] HttpRequest req)
    {
        var result = await mediator.Send(new GetCollectorAssignmentsQuery(req.Headers[DevUserIdHeader].FirstOrDefault()));
        return new OkObjectResult(new ApiResponse<CollectorAssignmentsDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetCollectorHistory")]
    public async Task<IActionResult> GetHistory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.History)] HttpRequest req)
    {
        var result = await mediator.Send(new ListCollectorHistoryQuery(req.Headers[DevUserIdHeader].FirstOrDefault()));
        return new OkObjectResult(new ApiResponse<IEnumerable<CollectorHistoryReceiptDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
