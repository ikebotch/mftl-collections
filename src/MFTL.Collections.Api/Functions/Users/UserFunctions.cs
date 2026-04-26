using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Features.Users.Queries.ListUsers;

namespace MFTL.Collections.Api.Functions.Users;

public class UserFunctions(IMediator mediator)
{
    [Function("GetUserById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Users.GetById)] HttpRequest req, Guid id)
    {
        var result = await mediator.Send(new Application.Features.Users.Queries.GetUserById.GetUserByIdQuery(id));
        return new OkObjectResult(new ApiResponse<UserDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("UpdateUser")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ApiRoutes.Users.Update)] HttpRequest req, Guid id)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = System.Text.Json.JsonSerializer.Deserialize<Application.Features.Users.Commands.UpdateUser.UpdateUserCommand>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (command == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(command with { Id = id });
        if (!result) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<bool>(true, "User updated.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
