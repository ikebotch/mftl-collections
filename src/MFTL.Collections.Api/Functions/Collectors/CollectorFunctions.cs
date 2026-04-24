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

    [Function("ListCollectors")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.AdminBase)] HttpRequest req)
    {
        var result = await mediator.Send(new Application.Features.Collectors.Queries.ListCollectors.ListCollectorsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<CollectorMeDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetCollectorById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.GetById)] HttpRequest req, Guid id)
    {
        // Placeholder for GetCollectorByIdQuery
        var result = await mediator.Send(new Application.Features.Collectors.Queries.ListCollectors.ListCollectorsQuery());
        var collector = result.FirstOrDefault(x => x.Id == id);
        if (collector == null) return new NotFoundResult();
        return new OkObjectResult(new ApiResponse<CollectorMeDto>(true, Data: collector, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("CreateCollector")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Collectors.AdminBase)] HttpRequest req)
    {
        var command = await req.ReadFromJsonAsync<Application.Features.Collectors.Commands.CreateCollector.CreateCollectorCommand>();
        if (command == null) return new BadRequestObjectResult(new ApiResponse<object>(false, Message: "Invalid request body"));
        
        var result = await mediator.Send(command);
        return new OkObjectResult(new ApiResponse<CollectorMeDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("UpdateCollector")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ApiRoutes.Collectors.Update)] HttpRequest req, Guid id)
    {
        var command = await req.ReadFromJsonAsync<Application.Features.Collectors.Commands.UpdateCollector.UpdateCollectorCommand>();
        if (command == null) return new BadRequestObjectResult(new ApiResponse<object>(false, Message: "Invalid request body"));
        
        var result = await mediator.Send(command with { Id = id });
        if (!result) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<bool>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
