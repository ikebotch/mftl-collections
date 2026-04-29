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

namespace MFTL.Collections.Api.Functions.Notifications;

public class NotificationTemplateFunctions(IMediator mediator)
{
    [Function("ListNotificationTemplates")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.NotificationTemplates.Base)] HttpRequest req)
    {
        var templateKey = req.Query["templateKey"].ToString();
        var channel = req.Query["channel"].ToString();

        var query = new ListNotificationTemplatesQuery
        {
            TemplateKey = string.IsNullOrEmpty(templateKey) ? null : templateKey,
            Channel = string.IsNullOrEmpty(channel) ? null : channel
        };

        var result = await mediator.Send(query);
        return new OkObjectResult(result);
    }

    [Function("GetNotificationTemplateById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.NotificationTemplates.GetById)] HttpRequest req, Guid id)
    {
        var result = await mediator.Send(new GetNotificationTemplateByIdQuery(id));
        if (result == null) return new NotFoundResult();
        return new OkObjectResult(result);
    }

    [Function("CreateNotificationTemplate")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.NotificationTemplates.Base)] HttpRequest req)
    {
        var command = await req.ReadFromJsonAsync<CreateNotificationTemplateCommand>();
        if (command == null) return new BadRequestResult();

        var result = await mediator.Send(command);
        return new OkObjectResult(result);
    }

    [Function("UpdateNotificationTemplate")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ApiRoutes.NotificationTemplates.GetById)] HttpRequest req, Guid id)
    {
        var command = await req.ReadFromJsonAsync<UpdateNotificationTemplateCommand>();
        if (command == null) return new BadRequestResult();
        
        var result = await mediator.Send(command with { Id = id });
        return new OkObjectResult(result);
    }

    [Function("PreviewNotificationTemplate")]
    public async Task<IActionResult> Preview(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.NotificationTemplates.Preview)] HttpRequest req, Guid id)
    {
        var variables = await req.ReadFromJsonAsync<Dictionary<string, string>>();
        var result = await mediator.Send(new PreviewNotificationTemplateQuery { Id = id, Variables = variables ?? new() });
        if (result == null) return new NotFoundResult();
        return new OkObjectResult(result);
    }

    [Function("SendTestNotification")]
    public async Task<IActionResult> SendTest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.NotificationTemplates.SendTest)] HttpRequest req, Guid id)
    {
        // Implementation for sending test message
        // For now, just return OK
        return new OkResult();
    }
}
