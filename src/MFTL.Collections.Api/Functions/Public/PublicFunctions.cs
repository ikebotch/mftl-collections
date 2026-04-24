using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Features.Public.Queries.GetEventBySlug;
using MFTL.Collections.Application.Features.Public.Queries.ListPublicRecipientFunds;
using MFTL.Collections.Application.Features.Public.Commands.InitiatePublicContribution;
using MFTL.Collections.Application.Features.Public.Queries.GetPublicPaymentStatus;
using MFTL.Collections.Application.Features.Public.Queries.GetPublicReceipt;
using System.Text.Json;

namespace MFTL.Collections.Api.Functions.Public;

public class PublicFunctions(IMediator mediator)
{
    [Function("GetPublicEventBySlug")]
    public async Task<IActionResult> GetEventBySlug(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/public/events/{slug}")] HttpRequest req, string slug)
    {
        var result = await mediator.Send(new GetEventBySlugQuery(slug));
        if (result == null) return new NotFoundObjectResult(new ApiResponse(false, "Event not found.", CorrelationId: req.GetOrCreateCorrelationId()));
        
        return new OkObjectResult(new ApiResponse<PublicEventDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("ListPublicRecipientFunds")]
    public async Task<IActionResult> ListRecipientFunds(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/public/events/{slug}/recipient-funds")] HttpRequest req, string slug)
    {
        var result = await mediator.Send(new ListPublicRecipientFundsQuery(slug));
        return new OkObjectResult(new ApiResponse<List<PublicRecipientFundDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("InitiatePublicContribution")]
    public async Task<IActionResult> InitiateContribution(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/public/contributions/initiate")] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<InitiatePublicContributionRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (request == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(new InitiatePublicContributionCommand(
            request.EventSlug,
            request.RecipientFundId,
            request.Amount,
            request.Currency,
            request.ContributorName,
            request.ContributorEmail,
            request.ContributorPhone,
            request.IsAnonymous,
            request.Method,
            request.Note));
            
        return new OkObjectResult(new ApiResponse<PaymentResult>(result.Success, result.Error, result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetPublicPaymentStatus")]
    public async Task<IActionResult> GetPaymentStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/public/payments/{paymentId}/status")] HttpRequest req, Guid paymentId)
    {
        var result = await mediator.Send(new GetPublicPaymentStatusQuery(paymentId));
        if (result == null) return new NotFoundObjectResult(new ApiResponse(false, "Payment not found.", CorrelationId: req.GetOrCreateCorrelationId()));
        
        return new OkObjectResult(new ApiResponse<PublicPaymentStatusDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetPublicReceipt")]
    public async Task<IActionResult> GetReceipt(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/public/receipts/{receiptId}")] HttpRequest req, Guid receiptId)
    {
        var result = await mediator.Send(new GetPublicReceiptQuery(receiptId));
        if (result == null) return new NotFoundObjectResult(new ApiResponse(false, "Receipt not found.", CorrelationId: req.GetOrCreateCorrelationId()));
        
        return new OkObjectResult(new ApiResponse<PublicReceiptDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetPublicReceiptByContribution")]
    public async Task<IActionResult> GetReceiptByContribution(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/public/receipts/by-contribution/{contributionId}")] HttpRequest req, Guid contributionId)
    {
        var result = await mediator.Send(new GetPublicReceiptByContributionQuery(contributionId));
        if (result == null) return new NotFoundObjectResult(new ApiResponse(false, "Receipt not found yet.", CorrelationId: req.GetOrCreateCorrelationId()));
        
        return new OkObjectResult(new ApiResponse<PublicReceiptDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}

public record InitiatePublicContributionRequest(
    string EventSlug,
    Guid RecipientFundId,
    decimal Amount,
    string Currency,
    string ContributorName,
    string? ContributorEmail,
    string? ContributorPhone,
    bool IsAnonymous,
    string Method,
    string? Note);
