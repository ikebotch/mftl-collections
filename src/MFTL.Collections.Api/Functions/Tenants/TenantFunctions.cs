using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Features.Tenants.Queries.ListTenants;

namespace MFTL.Collections.Api.Functions.Tenants;

public class TenantFunctions(IMediator mediator)
{
    [Function("ListTenants")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Tenants.Base)] HttpRequest req)
    {
        var result = await mediator.Send(new ListTenantsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<TenantDto>>(true, "Tenants retrieved.", result));
    }

    [Function("CreateTenant")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Tenants.Base)] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = System.Text.Json.JsonSerializer.Deserialize<Application.Features.Tenants.Commands.CreateTenant.CreateTenantCommand>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (command == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body."));

        var result = await mediator.Send(command);
        return new OkObjectResult(new ApiResponse<Guid>(true, "Tenant created.", result));
    }

    [Function("UpdateTenant")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ApiRoutes.Tenants.Update)] HttpRequest req,
        string id)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = System.Text.Json.JsonSerializer.Deserialize<Application.Features.Tenants.Commands.UpdateTenant.UpdateTenantCommand>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (command == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body."));
        
        if (command.Id == Guid.Empty)
        {
             command = command with { Id = Guid.Parse(id) };
        }

        var result = await mediator.Send(command);
        return result ? new OkObjectResult(new ApiResponse(true, "Tenant updated.")) : new NotFoundObjectResult(new ApiResponse(false, "Tenant not found."));
    }
}
