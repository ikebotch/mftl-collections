using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Contracts.Common;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MFTL.Collections.Application.Features.Webhooks.Auth0.Commands.UserCreated;

namespace MFTL.Collections.Api.Functions.Webhooks;

public class Auth0WebhookFunction(IMediator mediator, IConfiguration configuration)
{
    [Function("Auth0UserCreatedWebhook")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Webhooks.Auth0)] HttpRequest req)
    {
        var secret = configuration["Values:AUTH0_WEBHOOK_SECRET"] ?? configuration["AUTH0_WEBHOOK_SECRET"];
        var receivedSecret = req.Headers["x-auth0-secret"].ToString();

        if (!string.IsNullOrEmpty(secret) && secret != receivedSecret)
        {
            return new UnauthorizedResult();
        }

        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<Auth0UserCreatedPayload>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data == null || string.IsNullOrEmpty(data.UserId) || string.IsNullOrEmpty(data.Email))
        {
            return new BadRequestResult();
        }

        await mediator.Send(new UserCreatedWebhookCommand(
            data.UserId,
            data.Email,
            data.Name ?? data.Email.Split('@')[0],
            data.IsPlatformAdmin));

        return new OkResult();
    }

    private record Auth0UserCreatedPayload(
        string UserId,
        string Email,
        string? Name,
        bool IsPlatformAdmin);
}
