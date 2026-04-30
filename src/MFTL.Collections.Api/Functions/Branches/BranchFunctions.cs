using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Features.Branches.Queries.ListBranches;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Api.Extensions;

namespace MFTL.Collections.Api.Functions.Branches;

public class BranchFunctions(
    IMediator mediator,
    IScopeAccessService scopeService,
    ITenantContext tenantContext)
{
    [Function("ListBranches")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Branches.Base)] HttpRequest req)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Branches.View, req);
        if (deny != null) return deny;

        var tenantIdStr = req.Query["tenantId"];
        Guid? tenantId = Guid.TryParse(tenantIdStr, out var tid) ? tid : null;
        
        var result = await mediator.Send(new ListBranchesQuery(tenantId));
        return new OkObjectResult(new ApiResponse<IEnumerable<BranchDto>>(true, "Branches retrieved.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetBranch")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Branches.GetById)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Branches.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new Application.Features.Branches.Queries.GetBranchById.GetBranchByIdQuery(id));
        if (result == null) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<BranchDto>(true, "Branch retrieved.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("CreateBranch")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Branches.Base)] HttpRequest req)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Branches.Create, req);
        if (deny != null) return deny;

        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = System.Text.Json.JsonSerializer.Deserialize<Application.Features.Branches.Commands.CreateBranch.CreateBranchCommand>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (command == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(command);
        return new OkObjectResult(new ApiResponse<Guid>(true, "Branch created.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("UpdateBranch")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ApiRoutes.Branches.Update)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Branches.Update, req);
        if (deny != null) return deny;

        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = System.Text.Json.JsonSerializer.Deserialize<Application.Features.Branches.Commands.UpdateBranch.UpdateBranchCommand>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (command == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(command with { Id = id });
        if (!result) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<bool>(true, "Branch updated.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("DeleteBranch")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = ApiRoutes.Branches.GetById)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Branches.Delete, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new Application.Features.Branches.Commands.DeleteBranch.DeleteBranchCommand(id));
        if (!result) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<bool>(true, "Branch deleted/deactivated.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
