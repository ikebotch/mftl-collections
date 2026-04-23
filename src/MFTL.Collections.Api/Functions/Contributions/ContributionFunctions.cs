using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using MediatR;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Features.Contributions.Commands.RecordCashContribution;
using Newtonsoft.Json;

namespace MFTL.Collections.Api.Functions.Contributions;

public class ContributionFunctions(IMediator mediator)
{
    [Function("RecordCashContribution")]
    [OpenApiOperation(operationId: "RecordCashContribution", tags: new[] { "Contributions" })]
    [OpenApiRequestBody("application/json", typeof(RecordCashContributionRequest))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(ApiResponse<Guid>))]
    public async Task<HttpResponseData> RecordCash(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Contributions.RecordCash)] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonConvert.DeserializeObject<RecordCashContributionRequest>(body);
        
        if (request == null)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteAsJsonAsync(new ApiResponse(false, "Invalid body."));
            return errorResponse;
        }

        var result = await mediator.Send(new RecordCashContributionCommand(
            request.EventId, 
            request.RecipientFundId, 
            request.Amount, 
            request.ContributorName ?? "Anonymous", 
            request.Note));
            
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new ApiResponse<Guid>(true, "Cash contribution recorded.", result));
        return response;
    }
}

public record RecordCashContributionRequest(Guid EventId, Guid RecipientFundId, decimal Amount, string? ContributorName, string? Note);
