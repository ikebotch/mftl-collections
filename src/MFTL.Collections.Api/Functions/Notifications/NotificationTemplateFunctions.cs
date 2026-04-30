using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Features.Notifications.Queries.ListNotificationTemplates;
using MFTL.Collections.Application.Features.Notifications.Queries.GetNotificationTemplateById;
using MFTL.Collections.Application.Features.Notifications.Commands.CreateNotificationTemplate;
using MFTL.Collections.Application.Features.Notifications.Commands.UpdateNotificationTemplate;
using MFTL.Collections.Application.Features.Notifications.Queries.PreviewNotificationTemplate;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Api.Functions.Notifications;

public class NotificationTemplateFunctions(
    IMediator mediator,
    IScopeAccessService scopeService,
    ITenantContext tenantContext)
{
    [Function("ListNotificationTemplates")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.NotificationTemplates.Base)] HttpRequest req)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.NotificationTemplates.View, req);
        if (deny != null) return deny;

        var templateKey = req.Query["templateKey"].ToString();
        var channel = req.Query["channel"].ToString();

        var query = new ListNotificationTemplatesQuery
        {
            TemplateKey = string.IsNullOrEmpty(templateKey) ? null : templateKey,
            Channel = string.IsNullOrEmpty(channel) ? null : channel
        };

        var result = await mediator.Send(query);
        return new OkObjectResult(new ApiResponse<IEnumerable<NotificationTemplateDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetNotificationTemplateById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.NotificationTemplates.GetById)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.NotificationTemplates.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new GetNotificationTemplateByIdQuery(id));
        if (result == null) return new NotFoundResult();
        return new OkObjectResult(new ApiResponse<NotificationTemplateDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("CreateNotificationTemplate")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.NotificationTemplates.Base)] HttpRequest req)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.NotificationTemplates.Create, req);
        if (deny != null) return deny;

        var command = await req.ReadFromJsonAsync<CreateNotificationTemplateCommand>();
        if (command == null) return new BadRequestResult();

        var result = await mediator.Send(command);
        return new OkObjectResult(new ApiResponse<Guid>(true, "Notification template created.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("UpdateNotificationTemplate")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ApiRoutes.NotificationTemplates.GetById)] HttpRequest req, Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.NotificationTemplates.Update, req);
        if (deny != null) return deny;

        var command = await req.ReadFromJsonAsync<UpdateNotificationTemplateCommand>();
        if (command == null) return new BadRequestResult();

        var result = await mediator.Send(command with { Id = id });
        if (!result) return new NotFoundResult();

        return new OkObjectResult(new ApiResponse<bool>(true, "Notification template updated.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("PreviewNotificationTemplate")]
    public async Task<IActionResult> Preview(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.NotificationTemplates.Preview)] HttpRequest req)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.NotificationTemplates.Test, req);
        if (deny != null) return deny;

        var query = await req.ReadFromJsonAsync<PreviewNotificationTemplateQuery>();
        if (query == null) return new BadRequestResult();

        var result = await mediator.Send(query);
        return new OkObjectResult(new ApiResponse<RenderedTemplateDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
