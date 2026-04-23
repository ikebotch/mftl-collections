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
using MFTL.Collections.Application.Features.Events.Queries.GetEventById;
using MFTL.Collections.Application.Features.Events.Queries.ListEvents;

namespace MFTL.Collections.Api.Functions.Events;

public class EventFunctions(IMediator mediator)
{
    [Function("GetEventById")]
    [OpenApiOperation(operationId: "GetEventById", tags: new[] { "Events" })]
    [OpenApiSecurity("Authorization", SecuritySchemeType.Http, Scheme = OpenApiSecuritySchemeType.Bearer, BearerFormat = "JWT")]
    [OpenApiParameter(name: "X-Tenant-Id", In = ParameterLocation.Header, Required = true, Type = typeof(Guid))]
    [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(ApiResponse<EventDto>))]
    public async Task<HttpResponseData> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Events.GetById)] HttpRequestData req, Guid id)
    {
        var result = await mediator.Send(new GetEventByIdQuery(id));
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new ApiResponse<EventDto>(true, Data: result));
        return response;
    }

    [Function("ListEvents")]
    [OpenApiOperation(operationId: "ListEvents", tags: new[] { "Events" })]
    [OpenApiSecurity("Authorization", SecuritySchemeType.Http, Scheme = OpenApiSecuritySchemeType.Bearer, BearerFormat = "JWT")]
    [OpenApiParameter(name: "X-Tenant-Id", In = ParameterLocation.Header, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(ApiResponse<IEnumerable<EventDto>>))]
    public async Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Events.Base)] HttpRequestData req)
    {
        var result = await mediator.Send(new ListEventsQuery());
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new ApiResponse<IEnumerable<EventDto>>(true, Data: result));
        return response;
    }
}
