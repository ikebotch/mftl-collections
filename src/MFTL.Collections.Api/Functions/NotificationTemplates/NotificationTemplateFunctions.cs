using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using System.Text.Json;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Features.NotificationTemplates.Commands;
using MFTL.Collections.Application.Features.NotificationTemplates.Queries;


namespace MFTL.Collections.Api.Functions.NotificationTemplates;

public class NotificationTemplateFunctions(IMediator mediator)
{
    [Function("ListNotificationTemplates")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.NotificationTemplates.Base)] HttpRequest req)
    {
        var templateKey = req.Query["templateKey"].FirstOrDefault();
        var channelStr = req.Query["channel"].FirstOrDefault();
        Domain.Enums.NotificationChannel? channel = null;
        if (!string.IsNullOrWhiteSpace(channelStr) &&
            Enum.TryParse<Domain.Enums.NotificationChannel>(channelStr, true, out var parsedChannel))
            channel = parsedChannel;

        var result = await mediator.Send(new ListNotificationTemplatesQuery(templateKey, channel));
        return new OkObjectResult(new ApiResponse<List<NotificationTemplateDto>>(
            true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetNotificationTemplateById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.NotificationTemplates.GetById)] HttpRequest req, Guid id)
    {
        var result = await mediator.Send(new GetNotificationTemplateByIdQuery(id));
        if (result == null) return new NotFoundResult();
        return new OkObjectResult(new ApiResponse<NotificationTemplateDto>(
            true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("CreateNotificationTemplate")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.NotificationTemplates.Base)] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = JsonSerializer.Deserialize<CreateNotificationTemplateCommand>(
            body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (command == null)
            return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.",
                CorrelationId: req.GetOrCreateCorrelationId()));

        var id = await mediator.Send(command);
        return new OkObjectResult(new ApiResponse<Guid>(
            true, "Notification template created.", id, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("UpdateNotificationTemplate")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ApiRoutes.NotificationTemplates.Update)] HttpRequest req, Guid id)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = JsonSerializer.Deserialize<UpdateNotificationTemplateCommand>(
            body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (command == null)
            return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.",
                CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(command with { Id = id });
        if (!result) return new NotFoundResult();
        return new OkObjectResult(new ApiResponse<bool>(
            true, "Notification template updated.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("PreviewNotificationTemplate")]
    public async Task<IActionResult> Preview(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.NotificationTemplates.Preview)] HttpRequest req, Guid id)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var variables = string.IsNullOrWhiteSpace(body)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(
                body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
              ?? new Dictionary<string, string>();

        var result = await mediator.Send(new PreviewNotificationTemplateQuery(id, variables));
        if (result == null) return new NotFoundResult();
        return new OkObjectResult(new ApiResponse<RenderedTemplateDto>(
            true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
