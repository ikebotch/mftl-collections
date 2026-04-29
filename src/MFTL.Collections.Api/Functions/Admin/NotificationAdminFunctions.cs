using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MFTL.Collections.Api.Functions.Admin;

public class NotificationAdminFunctions(IMediator mediator, ILogger<NotificationAdminFunctions> logger)
{
    [Function("GetOutboxMessages")]
    public async Task<IActionResult> GetOutboxMessages(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/outbox-events")] HttpRequest req)
    {
        // TODO: Implement ListOutboxMessagesQuery
        return new OkObjectResult(new { message = "List of outbox messages" });
    }

    [Function("RetryOutboxMessage")]
    public async Task<IActionResult> RetryOutboxMessage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/outbox-events/{id}/retry")] HttpRequest req,
        string id)
    {
        if (!Guid.TryParse(id, out var guid)) return new BadRequestObjectResult("Invalid ID format");
        
        var success = await mediator.Send(new Application.Features.Admin.Notifications.Commands.RetryOutboxMessage.RetryOutboxMessageCommand(guid));
        return success ? new OkResult() : new NotFoundResult();
    }

    [Function("GetNotifications")]
    public async Task<IActionResult> GetNotifications(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/notifications")] HttpRequest req)
    {
        // TODO: Implement ListNotificationsQuery
        return new OkObjectResult(new { message = "List of notifications" });
    }
}
