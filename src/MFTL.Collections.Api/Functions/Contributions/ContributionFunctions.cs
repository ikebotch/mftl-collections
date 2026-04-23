using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Contracts.Common;
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
        
        if (request == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body."));

        var result = await mediator.Send(new RecordCashContributionCommand(
            request.EventId, 
            request.RecipientFundId, 
            request.Amount, 
            request.ContributorName ?? "Anonymous", 
            request.Note));
            
        return new OkObjectResult(new ApiResponse<Guid>(true, "Cash contribution recorded.", result));
    }
}

public record RecordCashContributionRequest(Guid EventId, Guid RecipientFundId, decimal Amount, string? ContributorName, string? Note);
