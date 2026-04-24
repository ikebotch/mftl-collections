using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Features.Events.Queries.GetEventById;
using MFTL.Collections.Application.Features.Events.Queries.ListEvents;
using MFTL.Collections.Application.Features.Events.Commands.UpdateEvent;

namespace MFTL.Collections.Api.Functions.Events;

public class EventFunctions(IMediator mediator)
{
    [Function("GetEventById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Events.GetById)] HttpRequest req, Guid id)
    {
        var result = await mediator.Send(new GetEventByIdQuery(id));
        return new OkObjectResult(new ApiResponse<EventDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("ListEvents")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Events.Base)] HttpRequest req)
    {
        var result = await mediator.Send(new ListEventsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<EventDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("UpdateEvent")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ApiRoutes.Events.Update)] HttpRequest req, Guid id)
    {
        var request = await req.ReadFromJsonAsync<UpdateEventRequest>();
        if (request == null) return new BadRequestObjectResult("Invalid request body");

        var result = await mediator.Send(new UpdateEventCommand(
            id,
            request.Title,
            request.Description,
            request.EventDate,
            request.IsActive,
            request.Slug));

        return new OkObjectResult(new ApiResponse<EventDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
