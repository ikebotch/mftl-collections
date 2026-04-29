using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Features.Admin.Notifications.Queries;
using MFTL.Collections.Application.Features.Admin.Notifications.Commands.RetryOutboxMessage;

namespace MFTL.Collections.Api.Functions.Admin;

public class NotificationAdminFunctions(IMediator mediator, ILogger<NotificationAdminFunctions> logger)
{
    [Function("GetOutboxMessages")]
    public async Task<IActionResult> GetOutboxMessages(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/outbox-events")] HttpRequest req)
    {
        var pageNumber = int.TryParse(req.Query["pageNumber"], out var pn) ? pn : 1;
        var pageSize = int.TryParse(req.Query["pageSize"], out var ps) ? ps : 10;
        var status = req.Query["status"];

        var result = await mediator.Send(new GetOutboxMessagesQuery(pageNumber, pageSize, status));
        return new OkObjectResult(result);
    }

    [Function("RetryOutboxMessage")]
    public async Task<IActionResult> RetryOutboxMessage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/outbox-events/{id}/retry")] HttpRequest req,
        string id)
    {
        if (!Guid.TryParse(id, out var guid)) return new BadRequestObjectResult("Invalid ID format");
        
        var success = await mediator.Send(new RetryOutboxMessageCommand(guid));
        return success ? new OkResult() : new NotFoundResult();
    }

    [Function("GetNotifications")]
    public async Task<IActionResult> GetNotifications(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/notifications")] HttpRequest req)
    {
        var pageNumber = int.TryParse(req.Query["pageNumber"], out var pn) ? pn : 1;
        var pageSize = int.TryParse(req.Query["pageSize"], out var ps) ? ps : 10;
        var status = req.Query["status"];
        var channel = req.Query["channel"];

        var result = await mediator.Send(new GetNotificationsQuery(pageNumber, pageSize, status, channel));
        return new OkObjectResult(result);
    }
}
