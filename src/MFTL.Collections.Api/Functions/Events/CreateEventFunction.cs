using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MediatR;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Features.Events.Commands.CreateEvent;
using System.Text.Json;

namespace MFTL.Collections.Api.Functions.Events;

public class CreateEventFunction(IMediator mediator, ILogger<CreateEventFunction> logger)
{
    [Function("CreateEvent")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Events.Base)] HttpRequest req)
    {
        logger.LogInformation("Processing CreateEvent request.");

        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var createEventRequest = JsonSerializer.Deserialize<CreateEventRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (createEventRequest == null)
        {
            return new BadRequestObjectResult(new ApiResponse(false, "Invalid request body."));
        }

        var command = new CreateEventCommand(createEventRequest.Title, createEventRequest.Description, createEventRequest.EventDate);
        var result = await mediator.Send(command);

        return new OkObjectResult(new ApiResponse<EventDto>(true, "Event created successfully.", result));
    }
}
