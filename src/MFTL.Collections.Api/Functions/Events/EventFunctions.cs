using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Features.Events.Queries.GetEventById;
using MFTL.Collections.Application.Features.Events.Queries.ListEvents;
using MFTL.Collections.Application.Features.Events.Commands.UpdateEvent;
using MFTL.Collections.Application.Features.Events.Commands.AssignStaff;
using MFTL.Collections.Application.Features.Events.Commands.CreateEvent;

namespace MFTL.Collections.Api.Functions.Events;

public class EventFunctions(
    IMediator mediator,
    IScopeAccessService scopeService,
    ITenantContext tenantContext)
{
    [Function("GetEventById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Events.GetById)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Events.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new GetEventByIdQuery(id));
        return new OkObjectResult(new ApiResponse<EventDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("ListEvents")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Events.Base)] HttpRequest req)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Events.View, req);
        if (deny != null) return deny;

        var branchIdStr = req.Query["branchId"];
        Guid? branchId = Guid.TryParse(branchIdStr, out var bid) ? bid : null;

        var result = await mediator.Send(new ListEventsQuery(branchId));
        return new OkObjectResult(new ApiResponse<IEnumerable<EventDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("UpdateEvent")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ApiRoutes.Events.Update)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Events.Update, req);
        if (deny != null) return deny;

        var request = await req.ReadFromJsonAsync<UpdateEventRequest>();
        if (request == null) return new BadRequestObjectResult("Invalid request body");

        var result = await mediator.Send(new UpdateEventCommand(
            id,
            request.Title,
            request.Description,
            request.EventDate,
            request.IsActive,
            request.BranchId,
            request.Slug,
            request.DisplayImageUrl,
            request.ReceiptLogoUrl));

        return new OkObjectResult(new ApiResponse<EventDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("AssignStaffToEvent")]
    public async Task<IActionResult> AssignStaff(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Events.AssignStaff)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Events.AssignStaff, req);
        if (deny != null) return deny;

        var request = await req.ReadFromJsonAsync<IEnumerable<Guid>>();
        if (request == null) return new BadRequestObjectResult("Invalid request body. Expected a list of User IDs.");

        var result = await mediator.Send(new AssignStaffToEventCommand(id, request));
        return new OkObjectResult(new ApiResponse<bool>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("CreateEvent")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Events.Base)] HttpRequest req)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Events.Create, req);
        if (deny != null) return deny;

        var command = await req.ReadFromJsonAsync<CreateEventCommand>();
        if (command == null) return new BadRequestObjectResult("Invalid request body");

        var result = await mediator.Send(command);
        return new OkObjectResult(new ApiResponse<EventDto>(true, "Event created.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
