using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using MediatR;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Features.RecipientFunds.Commands.CreateRecipientFund;
using MFTL.Collections.Application.Features.RecipientFunds.Queries.ListRecipientFundsByEvent;
using Newtonsoft.Json;

namespace MFTL.Collections.Api.Functions.RecipientFunds;

public class RecipientFundFunctions(IMediator mediator)
{
    [Function("CreateRecipientFund")]
    [OpenApiOperation(operationId: "CreateRecipientFund", tags: new[] { "RecipientFunds" })]
    [OpenApiRequestBody("application/json", typeof(CreateRecipientFundRequest))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(ApiResponse<Guid>))]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.RecipientFunds.Base)] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonConvert.DeserializeObject<CreateRecipientFundRequest>(body);
        
        if (request == null)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteAsJsonAsync(new ApiResponse(false, "Invalid body."));
            return errorResponse;
        }

        var result = await mediator.Send(new CreateRecipientFundCommand(
            request.EventId, 
            request.Name, 
            request.Description, 
            request.TargetAmount, 
            request.Metadata));
            
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new ApiResponse<Guid>(true, "Recipient fund created.", result));
        return response;
    }

    [Function("ListRecipientFundsByEvent")]
    [OpenApiOperation(operationId: "ListRecipientFundsByEvent", tags: new[] { "RecipientFunds" })]
    [OpenApiParameter(name: "eventId", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(ApiResponse<IEnumerable<RecipientFundDto>>))]
    [OpenApiResponseWithBody(HttpStatusCode.BadRequest, "application/json", typeof(ApiResponse))]
    public async Task<HttpResponseData> ListByEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.RecipientFunds.ListByEvent)] HttpRequestData req, string eventId)
    {
        if (!Guid.TryParse(eventId, out var parsedEventId))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new ApiResponse(
                false,
                "Invalid eventId route parameter.",
                new[] { $"'{eventId}' is not a valid GUID." }));
            return badRequest;
        }

        var result = await mediator.Send(new ListRecipientFundsByEventQuery(parsedEventId));
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new ApiResponse<IEnumerable<RecipientFundDto>>(true, Data: result));
        return response;
    }
}

public record CreateRecipientFundRequest(Guid EventId, string Name, string? Description, decimal TargetAmount, string? Metadata);
