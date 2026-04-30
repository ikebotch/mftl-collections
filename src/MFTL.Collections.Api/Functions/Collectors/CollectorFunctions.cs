using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Azure.Functions.Worker;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorAssignments;
using MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorMe;
using MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorSettlements;
using MFTL.Collections.Application.Features.Collectors.Queries.ListCollectorHistory;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Api.Functions.Collectors;

public class CollectorFunctions(
    IMediator mediator,
    IScopeAccessService scopeService,
    ITenantContext tenantContext)
{
    [Function("GetCollectorMe")]
    public async Task<IActionResult> GetMe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.Me)] HttpRequest req)
    {
        // Require authentication. Identity is derived from CurrentUserService in the handler.
        await req.HttpContext.AuthenticateAsync();
        
        var result = await mediator.Send(new GetCollectorMeQuery());
        return new OkObjectResult(new ApiResponse<CollectorMeDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetCollectorAssignments")]
    public async Task<IActionResult> GetAssignments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.Assignments)] HttpRequest req)
    {
        // Require authentication. Identity is derived from CurrentUserService in the handler.
        await req.HttpContext.AuthenticateAsync();
        
        var result = await mediator.Send(new GetCollectorAssignmentsQuery());
        return new OkObjectResult(new ApiResponse<CollectorAssignmentsDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetCollectorHistory")]
    public async Task<IActionResult> GetHistory(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.History)] HttpRequest req)
    {
        // Require authentication. Identity is derived from CurrentUserService in the handler.
        await req.HttpContext.AuthenticateAsync();
        
        var result = await mediator.Send(new ListCollectorHistoryQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<CollectorHistoryReceiptDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetCollectorSettlements")]
    public async Task<IActionResult> GetSettlements(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.Settlements)] HttpRequest req)
    {
        // Require authentication. Identity is derived from CurrentUserService in the handler.
        await req.HttpContext.AuthenticateAsync();
        
        var result = await mediator.Send(new GetCollectorSettlementsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<SettlementDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("ListCollectors")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.AdminBase)] HttpRequest req)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Users.View, req);
        if (deny != null) return deny;

        Guid? eventId = null;
        if (req.Query.TryGetValue("eventId", out var eventIdStr) && Guid.TryParse(eventIdStr, out var parsedId))
        {
            eventId = parsedId;
        }

        var result = await mediator.Send(new Application.Features.Collectors.Queries.ListCollectors.ListCollectorsQuery(eventId));
        return new OkObjectResult(new ApiResponse<IEnumerable<CollectorMeDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetCollectorById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.GetById)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Users.View, req);
        if (deny != null) return deny;

        // Placeholder logic - should ideally have a dedicated GetCollectorByIdQuery
        var result = await mediator.Send(new Application.Features.Collectors.Queries.ListCollectors.ListCollectorsQuery());
        var collector = result.FirstOrDefault(x => x.Id == id);
        if (collector == null) return new NotFoundResult();
        return new OkObjectResult(new ApiResponse<CollectorMeDto>(true, Data: collector, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("CreateCollector")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Collectors.AdminBase)] HttpRequest req)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Users.Invite, req);
        if (deny != null) return deny;

        var command = await req.ReadFromJsonAsync<Application.Features.Collectors.Commands.CreateCollector.CreateCollectorCommand>();
        if (command == null) return new BadRequestObjectResult(new ApiResponse<object>(false, Message: "Invalid request body"));
        
        var result = await mediator.Send(command);
        return new OkObjectResult(new ApiResponse<CollectorMeDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("UpdateCollector")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ApiRoutes.Collectors.Update)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Users.Update, req);
        if (deny != null) return deny;

        var command = await req.ReadFromJsonAsync<Application.Features.Collectors.Commands.UpdateCollector.UpdateCollectorCommand>();
        if (command == null) return new BadRequestObjectResult(new ApiResponse<object>(false, Message: "Invalid request body"));
        
        var result = await mediator.Send(command with { Id = id });
        if (!result) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<bool>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
