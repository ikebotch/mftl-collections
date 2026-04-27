using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Features.Branches.Queries.ListBranches;

namespace MFTL.Collections.Api.Functions.Branches;

public class BranchFunctions(IMediator mediator)
{
    [Function("ListBranches")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Branches.Base)] HttpRequest req)
    {
        var tenantIds = req.Query["tenantId"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .ToList();
        
        var result = await mediator.Send(new ListBranchesQuery(tenantIds.Count > 0 ? tenantIds : null));
        return new OkObjectResult(new ApiResponse<IEnumerable<BranchDto>>(true, "Branches retrieved.", result));
    }

    [Function("GetBranch")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Branches.GetById)] HttpRequest req, Guid id)
    {
        var result = await mediator.Send(new Application.Features.Branches.Queries.GetBranchById.GetBranchByIdQuery(id));
        if (result == null) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<Application.Features.Branches.Queries.ListBranches.BranchDto>(true, "Branch retrieved.", result));
    }

    [Function("CreateBranch")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Branches.Base)] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = System.Text.Json.JsonSerializer.Deserialize<Application.Features.Branches.Commands.CreateBranch.CreateBranchCommand>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (command == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body."));

        var result = await mediator.Send(command);
        return new OkObjectResult(new ApiResponse<Guid>(true, "Branch created.", result));
    }

    [Function("UpdateBranch")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ApiRoutes.Branches.Update)] HttpRequest req, Guid id)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = System.Text.Json.JsonSerializer.Deserialize<Application.Features.Branches.Commands.UpdateBranch.UpdateBranchCommand>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (command == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body."));

        var result = await mediator.Send(command with { Id = id });
        if (!result) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<bool>(true, "Branch updated.", result));
    }

    [Function("DeactivateBranch")]
    public async Task<IActionResult> Deactivate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/branches/{id}/deactivate")] HttpRequest req, Guid id)
    {
        var result = await mediator.Send(new Application.Features.Branches.Commands.UpdateBranch.UpdateBranchCommand(Id: id, IsActive: false));
        if (!result) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<bool>(true, "Branch deactivated.", result));
    }

    [Function("DeleteBranch")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = ApiRoutes.Branches.GetById)] HttpRequest req, Guid id)
    {
        var result = await mediator.Send(new Application.Features.Branches.Commands.DeleteBranch.DeleteBranchCommand(id));
        if (!result) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<bool>(true, "Branch deleted.", result));
    }
}
