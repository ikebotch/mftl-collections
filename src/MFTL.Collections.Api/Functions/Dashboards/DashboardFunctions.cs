using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Features.Dashboards.Queries.GetRecipientDashboard;
using MFTL.Collections.Application.Features.Dashboards.Queries.GetEventDashboard;

namespace MFTL.Collections.Api.Functions.Dashboards;

public class DashboardFunctions(IMediator mediator)
{
    [Function("GetRecipientDashboard")]
    public async Task<IActionResult> GetRecipientDashboard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Dashboards.Recipient)] HttpRequest req, Guid id)
    {
        var result = await mediator.Send(new GetRecipientDashboardQuery(id));
        return new OkObjectResult(new ApiResponse<RecipientDashboardDto>(true, Data: result));
    }

    [Function("GetEventDashboard")]
    public async Task<IActionResult> GetEventDashboard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Dashboards.Event)] HttpRequest req, Guid id)
    {
        var result = await mediator.Send(new GetEventDashboardQuery(id));
        return new OkObjectResult(new ApiResponse<EventDashboardDto>(true, Data: result));
    }
}
