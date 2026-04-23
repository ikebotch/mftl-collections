using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using MediatR;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Features.Dashboards.Queries.GetRecipientDashboard;
using MFTL.Collections.Application.Features.Dashboards.Queries.GetEventDashboard;

namespace MFTL.Collections.Api.Functions.Dashboards;

public class DashboardFunctions(IMediator mediator)
{
    [Function("GetRecipientDashboard")]
    [OpenApiOperation(operationId: "GetRecipientDashboard", tags: new[] { "Dashboards" })]
    [OpenApiSecurity("Authorization", SecuritySchemeType.Http, Scheme = OpenApiSecuritySchemeType.Bearer, BearerFormat = "JWT")]
    [OpenApiParameter(name: "X-Tenant-Id", In = ParameterLocation.Header, Required = true, Type = typeof(Guid))]
    [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(ApiResponse<RecipientDashboardDto>))]
    public async Task<HttpResponseData> GetRecipientDashboard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Dashboards.Recipient)] HttpRequestData req, Guid id)
    {
        var result = await mediator.Send(new GetRecipientDashboardQuery(id));
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new ApiResponse<RecipientDashboardDto>(true, Data: result));
        return response;
    }

    [Function("GetEventDashboard")]
    [OpenApiOperation(operationId: "GetEventDashboard", tags: new[] { "Dashboards" })]
    [OpenApiSecurity("Authorization", SecuritySchemeType.Http, Scheme = OpenApiSecuritySchemeType.Bearer, BearerFormat = "JWT")]
    [OpenApiParameter(name: "X-Tenant-Id", In = ParameterLocation.Header, Required = true, Type = typeof(Guid))]
    [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(ApiResponse<EventDashboardDto>))]
    public async Task<HttpResponseData> GetEventDashboard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Dashboards.Event)] HttpRequestData req, Guid id)
    {
        var result = await mediator.Send(new GetEventDashboardQuery(id));
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new ApiResponse<EventDashboardDto>(true, Data: result));
        return response;
    }
}
