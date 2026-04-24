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
    [Function("ListUsers")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Users.Base)] HttpRequest req)
    {
        var result = await mediator.Send(new ListUsersQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<UserDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
