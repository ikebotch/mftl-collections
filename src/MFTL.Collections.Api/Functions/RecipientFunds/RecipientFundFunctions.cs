using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Features.RecipientFunds.Commands.CreateRecipientFund;
using MFTL.Collections.Application.Features.RecipientFunds.Queries.ListRecipientFundsByEvent;
using System.Text.Json;

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

    [Function("ListRecipientFundsByEvent")]
    public async Task<IActionResult> ListByEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.RecipientFunds.ListByEvent)] HttpRequest req, Guid eventId)
    {
        var result = await mediator.Send(new ListRecipientFundsByEventQuery(eventId));
        return new OkObjectResult(new ApiResponse<IEnumerable<RecipientFundDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}

public record CreateRecipientFundRequest(Guid EventId, string Name, string? Description, decimal TargetAmount, string? Metadata);
