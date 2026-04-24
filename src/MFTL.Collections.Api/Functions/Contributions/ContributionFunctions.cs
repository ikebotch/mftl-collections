using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Application.Features.Contributions.Queries.GetContributionById;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Features.Contributions.Commands.RecordCashContribution;
using System.Text.Json;

namespace MFTL.Collections.Api.Functions.Contributions;

public class ContributionFunctions(IMediator mediator)
{
    [Function("RecordCashContribution")]
    public async Task<IActionResult> RecordCash(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Contributions.RecordCash)] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<RecordCashContributionRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (request == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(new RecordCashContributionCommand(
            request.EventId, 
            request.RecipientFundId, 
            request.Amount, 
            request.ContributorName ?? "Anonymous", 
            request.Note));
            
        return new OkObjectResult(new ApiResponse<CashContributionResult>(true, "Cash contribution recorded.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetContributionById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Contributions.GetById)] HttpRequest req, Guid id)
    {
        var result = await mediator.Send(new GetContributionByIdQuery(id));
        return new OkObjectResult(new ApiResponse<ContributionDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("ListContributions")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Contributions.Base)] HttpRequest req)
    {
        var result = await mediator.Send(new MFTL.Collections.Application.Features.Contributions.Queries.ListContributions.ListContributionsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<ContributionDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}

public record RecordCashContributionRequest(Guid EventId, Guid RecipientFundId, decimal Amount, string? ContributorName, string? Note);
