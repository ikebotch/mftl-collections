using MFTL.Collections.Contracts.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Features.Payments.Commands.InitiateContributionPayment;
using MFTL.Collections.Application.Features.Payments.Queries.GetPaymentById;
using MFTL.Collections.Application.Features.Payments.Queries.ListPayments;
using System.Text.Json;

namespace MFTL.Collections.Api.Functions.Payments;

public class PaymentFunctions(
    IMediator mediator,
    IScopeAccessService scopeService,
    ITenantContext tenantContext)
{
    [Function("InitiateContributionPayment")]
    public async Task<IActionResult> Initiate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Payments.Initiate)] HttpRequest req)
    {
        // Public/Storefront can initiate payments, but we check if they have the initiate permission.
        // Usually, collectors or donors initiate. For storefront, we might need a bypass or specific permission.
        // Given the requirement "No resource bleed", we check if they have initiate permission in the tenant context.
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Payments.Initiate, req);
        if (deny != null) return deny;

        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<InitiatePaymentRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (request == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(new InitiateContributionPaymentCommand(request.ContributionId, request.PaymentMethod));
        return new OkObjectResult(new ApiResponse<PaymentResult>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("ListPayments")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Payments.Base)] HttpRequest req)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Payments.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new ListPaymentsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<PaymentDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetPaymentById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Payments.GetById)] HttpRequest req,
        Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Payments.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new GetPaymentByIdQuery(id));
        return new OkObjectResult(new ApiResponse<PaymentDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}

public record InitiatePaymentRequest(Guid ContributionId, string PaymentMethod);
