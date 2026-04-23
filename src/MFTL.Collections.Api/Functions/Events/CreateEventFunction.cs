using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using MediatR;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Features.Events.Commands.CreateEvent;
using Newtonsoft.Json;

namespace MFTL.Collections.Api.Functions.Events;

public class CreateEventFunction(IMediator mediator, ILogger<CreateEventFunction> logger)
{
    [Function("CreateEvent")]
    [OpenApiOperation(operationId: "CreateEvent", tags: new[] { "Events" })]
    [OpenApiSecurity("Authorization", SecuritySchemeType.Http, Scheme = OpenApiSecuritySchemeType.Bearer, BearerFormat = "JWT")]
    [OpenApiParameter(name: "X-Tenant-Id", In = ParameterLocation.Header, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody("application/json", typeof(CreateEventRequest))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(ApiResponse<EventDto>))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Events.Base)] HttpRequestData req)
    {
        logger.LogInformation("Processing CreateEvent request.");

        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var createEventRequest = JsonConvert.DeserializeObject<CreateEventRequest>(requestBody);

        if (createEventRequest == null)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteAsJsonAsync(new ApiResponse(false, "Invalid request body."));
            return errorResponse;
        }

        var command = new CreateEventCommand(createEventRequest.Title, createEventRequest.Description, createEventRequest.EventDate);
        var result = await mediator.Send(command);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new ApiResponse<EventDto>(true, "Event created successfully.", result));
        return response;
    }
}
