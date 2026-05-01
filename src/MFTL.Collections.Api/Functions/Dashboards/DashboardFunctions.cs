using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Features.Dashboards.Queries.GetRecipientDashboard;
using MFTL.Collections.Application.Features.Dashboards.Queries.GetEventDashboard;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Api.Extensions;

namespace MFTL.Collections.Api.Functions.Dashboards;

public class DashboardFunctions(
    IMediator mediator,
    IScopeAccessService scopeService,
    ITenantContext tenantContext)
{
    [Function("GetRecipientDashboard")]
    public async Task<IActionResult> GetRecipientDashboard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Dashboards.Recipient)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Dashboard.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new GetRecipientDashboardQuery(id));
        return new OkObjectResult(new ApiResponse<RecipientDashboardDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetEventDashboard")]
    public async Task<IActionResult> GetEventDashboard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Dashboards.Event)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Dashboard.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new GetEventDashboardQuery(id));
        return new OkObjectResult(new ApiResponse<EventDashboardDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetAdminDashboard")]
    public async Task<IActionResult> GetAdminDashboard(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Dashboards.Admin)] HttpRequest req)
    {
        // Admin dashboard requires higher level view or platform manage?
        // Usually dashboard.view is enough for tenant admins to see their own.
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Dashboard.Admin, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new Application.Features.Dashboards.Queries.GetAdminDashboard.GetAdminDashboardQuery());
        return new OkObjectResult(new ApiResponse<AdminDashboardDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
