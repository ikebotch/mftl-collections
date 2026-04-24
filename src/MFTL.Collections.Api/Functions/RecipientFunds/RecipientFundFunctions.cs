using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Common;
using System.Text.Json;
using MFTL.Collections.Application.Features.RecipientFunds.Commands.CreateRecipientFund;
using MFTL.Collections.Application.Features.RecipientFunds.Commands.UpdateRecipientFund;
using MFTL.Collections.Application.Features.RecipientFunds.Queries.GetRecipientFundById;
using MFTL.Collections.Application.Features.RecipientFunds.Queries.ListRecipientFunds;
using MFTL.Collections.Application.Features.RecipientFunds.Queries.ListRecipientFundsByEvent;

namespace MFTL.Collections.Api.Functions.RecipientFunds;

public class RecipientFundFunctions(IMediator mediator)
{
    [Function("CreateRecipientFund")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.RecipientFunds.Base)] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<CreateRecipientFundRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (request == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(new CreateRecipientFundCommand(
            request.EventId, 
            request.Name, 
            request.Description, 
            request.TargetAmount, 
            request.Metadata));
            
        return new OkObjectResult(new ApiResponse<Guid>(true, "Recipient fund created.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("UpdateRecipientFund")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ApiRoutes.RecipientFunds.Update)] HttpRequest req, Guid id)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<UpdateRecipientFundRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (request == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(new UpdateRecipientFundCommand(
            id,
            request.Name, 
            request.Description, 
            request.TargetAmount, 
            request.Metadata));
            
        if (!result) return new NotFoundResult();
            
        return new OkObjectResult(new ApiResponse(true, "Recipient fund updated.", CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetRecipientFundById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.RecipientFunds.GetById)] HttpRequest req, Guid id)
    {
        var result = await mediator.Send(new GetRecipientFundByIdQuery(id));
        if (result == null) return new NotFoundResult();
        return new OkObjectResult(new ApiResponse<RecipientFundDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("ListRecipientFunds")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.RecipientFunds.Base)] HttpRequest req)
    {
        var result = await mediator.Send(new ListRecipientFundsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<RecipientFundDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("ListRecipientFundsByEvent")]
    public async Task<IActionResult> ListByEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.RecipientFunds.ListByEvent)] HttpRequest req, Guid eventId)
    {
        var result = await mediator.Send(new ListRecipientFundsByEventQuery(eventId));
        return new OkObjectResult(new ApiResponse<IEnumerable<RecipientFundDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}

public record CreateRecipientFundRequest(Guid EventId, string Name, string? Description, decimal TargetAmount, string? Metadata);
public record UpdateRecipientFundRequest(string Name, string? Description, decimal TargetAmount, string? Metadata);
