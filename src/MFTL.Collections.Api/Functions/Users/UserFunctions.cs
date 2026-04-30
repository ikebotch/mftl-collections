using Microsoft.AspNetCore.Http;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using MediatR;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Features.Users.Queries.ListUsers;

namespace MFTL.Collections.Api.Functions.Users;

public class UserFunctions(IMediator mediator, IApplicationDbContext dbContext, ICurrentUserService currentUserService)
{
    private const string DevUserIdHeader = "X-Dev-User-Id";

    [Function("CoreListUsers")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Users.Base)] HttpRequest req)
    {
        var result = await mediator.Send(new ListUsersQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<UserDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetMe")]
    public async Task<IActionResult> GetMe(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Users.Me)] HttpRequest req)
    {
        // 0. Manually trigger authentication since middleware isn't run in isolated worker automatically
        var authResult = await req.HttpContext.AuthenticateAsync();
        if (authResult.Succeeded && authResult.Principal != null)
        {
            req.HttpContext.User = authResult.Principal;
        }

        // 1. Try to resolve the current user by their Auth0 sub claim
        // We check several possible locations for the user ID
        var auth0Id = currentUserService.UserId;
        
        if (string.IsNullOrWhiteSpace(auth0Id))
        {
            auth0Id = req.HttpContext?.User?.FindFirst("sub")?.Value 
                      ?? req.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }
        
        // 2. Fallback to dev header for local development bypass
        if (string.IsNullOrWhiteSpace(auth0Id))
        {
            auth0Id = req.Headers[DevUserIdHeader].FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(auth0Id))
        {
            var authHeader = req.Headers["Authorization"].FirstOrDefault();
            return new UnauthorizedObjectResult(new ApiResponse(false, 
                $"Authentication required. Claims present: {req.HttpContext?.User?.Claims.Count() ?? 0}. Auth header present: {!string.IsNullOrEmpty(authHeader)}", 
                CorrelationId: req.GetOrCreateCorrelationId()));
        }

        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id);

        if (user == null)
            return new NotFoundObjectResult(new ApiResponse(false, $"User profile not found for ID: {auth0Id}", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(new Application.Features.Users.Queries.GetUserById.GetUserByIdQuery(user.Id));
        return new OkObjectResult(new ApiResponse<UserDetailDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetUserById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Users.GetById)] HttpRequest req, Guid id)
    {
        var result = await mediator.Send(new Application.Features.Users.Queries.GetUserById.GetUserByIdQuery(id));
        return new OkObjectResult(new ApiResponse<UserDetailDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("InviteUser")]
    public async Task<IActionResult> Invite(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Users.Invite)] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = System.Text.Json.JsonSerializer.Deserialize<Application.Features.Users.Commands.InviteUser.InviteUserCommand>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (command == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        try 
        {
            var result = await mediator.Send(command);
            return new OkObjectResult(new ApiResponse<Guid>(true, "User invited.", result, CorrelationId: req.GetOrCreateCorrelationId()));
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(new ApiResponse(false, ex.Message, CorrelationId: req.GetOrCreateCorrelationId()));
        }
    }

    [Function("UpdateUserStatus")]
    public async Task<IActionResult> UpdateStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Users.UpdateStatus)] HttpRequest req, Guid id)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var command = System.Text.Json.JsonSerializer.Deserialize<Application.Features.Users.Commands.UpdateUserStatus.UpdateUserStatusCommand>(body, options);
        
        if (command == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(command with { Id = id });
        if (!result) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<bool>(true, "User status updated.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetUserAuditLogs")]
    public async Task<IActionResult> GetAudit(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Users.Audit)] HttpRequest req, Guid id)
    {
        var result = await mediator.Send(new Application.Features.Users.Queries.GetUserAuditLogs.GetUserAuditLogsQuery(id));
        return new OkObjectResult(new ApiResponse<IEnumerable<Application.Features.Users.Queries.GetUserAuditLogs.AuditLogDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
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

    [Function("AssignUserScope")]
    public async Task<IActionResult> AssignScope(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Users.Base + "/{id}/scopes")] HttpRequest req, Guid id)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = System.Text.Json.JsonSerializer.Deserialize<Application.Features.Users.Commands.AssignScope.AssignScopeCommand>(body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (command == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(command with { UserId = id });
        return new OkObjectResult(new ApiResponse<bool>(true, "Scope assigned.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("RevokeUserScope")]
    public async Task<IActionResult> RevokeScope(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = ApiRoutes.Users.Base + "/scopes/{assignmentId}")] HttpRequest req, Guid assignmentId)
    {
        var result = await mediator.Send(new Application.Features.Users.Commands.RevokeScope.RevokeScopeCommand(assignmentId));
        if (!result) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<bool>(true, "Scope revoked.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
