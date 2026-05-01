using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Features.Contributions.Commands.RecordCashContribution;
using MFTL.Collections.Application.Features.Contributions.Commands.UpdateContribution;
using MFTL.Collections.Application.Features.Contributions.Queries.GetContributionById;
using MFTL.Collections.Application.Features.Contributions.Queries.ListContributions;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using System.Text.Json;

using MFTL.Collections.Application.Features.Contributions.Commands.CreateContribution;

namespace MFTL.Collections.Api.Functions.Contributions;

public class ContributionFunctions(
    IMediator mediator,
    IScopeAccessService scopeService,
    ITenantContext tenantContext)
{
    [Function("CreateContribution")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Contributions.Base)] HttpRequest req)
    {
        // Public endpoint for donors to create a pending contribution.
        // No scope check required as the handler validates event/fund/tenant.
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = JsonSerializer.Deserialize<CreateContributionCommand>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (command == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(command);
        return new OkObjectResult(new ApiResponse<ContributionDto>(true, "Contribution initiated.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("RecordCashContribution")]
    public async Task<IActionResult> RecordCash(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Contributions.RecordCash)] HttpRequest req)
    {
        // Require contributions.create permission in the active scope
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Contributions.Create, req);
        if (deny != null) return deny;

        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<RecordCashContributionRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (request == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        // REMOVED: DevUserIdHeader / ExplicitUserId fallback.
        // Identity MUST come from CurrentUserService (enforced in the handler).
        var result = await mediator.Send(new RecordCashContributionCommand(
            request.EventId,
            request.RecipientFundId,
            request.Amount,
            request.Currency,
            request.ContributorName,
            request.ContributorPhone,
            request.ContributorEmail,
            request.Anonymous,
            request.PaymentMethod,
            request.Note,
            request.Reference,
            null)); // ExplicitUserId is now null, forcing handler to use current user.
            
        return new OkObjectResult(new ApiResponse<CashContributionResult>(true, "Cash contribution recorded.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetContributionById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Contributions.GetById)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Contributions.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new GetContributionByIdQuery(id));
        return new OkObjectResult(new ApiResponse<ContributionDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("ListContributions")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Contributions.Base)] HttpRequest req)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Contributions.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new ListContributionsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<ContributionListItemDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
    
    [Function("UpdateContribution")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ApiRoutes.Contributions.Update)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Contributions.Update, req);
        if (deny != null) return deny;

        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = JsonSerializer.Deserialize<UpdateContributionCommand>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (command == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(command with { Id = id });
        if (!result) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<bool>(true, "Contribution updated.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}

public record RecordCashContributionRequest(
    Guid EventId,
    Guid RecipientFundId,
    decimal Amount,
    string Currency,
    string? ContributorName,
    string ContributorPhone,
    string? ContributorEmail,
    bool Anonymous,
    string PaymentMethod,
    string? Note,
    string? Reference);
