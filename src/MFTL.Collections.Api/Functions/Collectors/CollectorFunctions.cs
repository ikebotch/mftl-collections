using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Features.Collectors.Queries.ListCollectors;
using MFTL.Collections.Application.Features.Collectors.Commands.CreateCollector;
using MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorMe;
using MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorAssignments;
using MFTL.Collections.Application.Features.Collectors.Queries.ListCollectorHistory;
using System.Text.Json;

namespace MFTL.Collections.Api.Functions.Collectors;

public class CollectorFunctions(IMediator mediator)
{
    [Function("ListCollectors")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.Base)] HttpRequest req)
    {
        var result = await mediator.Send(new ListCollectorsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<CollectorDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("CreateCollector")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Collectors.Base)] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<CreateCollectorRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (request == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(new CreateCollectorCommand(
            request.Name,
            request.Email,
            request.PhoneNumber,
            request.AssignedEventIds,
            request.AssignedFundIds));
            
        return new OkObjectResult(new ApiResponse<CollectorDto>(true, "Collector created successfully.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetCollectorMe")]
    public async Task<IActionResult> GetMe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.Me)] HttpRequest req)
    {
        var result = await mediator.Send(new GetCollectorMeQuery());
        return new OkObjectResult(new ApiResponse<CollectorMeDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetCollectorAssignments")]
    public async Task<IActionResult> GetAssignments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.Assignments)] HttpRequest req)
    {
        var result = await mediator.Send(new GetCollectorAssignmentsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<CollectorAssignmentDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetCollectorHistory")]
    public async Task<IActionResult> GetHistory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.History)] HttpRequest req)
    {
        var result = await mediator.Send(new ListCollectorHistoryQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<ReceiptListItemDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
