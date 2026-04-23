using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MFTL.Collections.Infrastructure.Payments;
using MFTL.Collections.Contracts.Common;
using System.Text.Json;

namespace MFTL.Collections.Api.Functions.Webhooks;

public class PaymentWebhookFunction(IPaymentWebhookProcessor webhookProcessor)
{
    [Function("PaymentWebhook")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Payments.Webhook + "/{provider}")] HttpRequest req, string provider)
    {
        var eventId = req.Headers["X-Event-Id"].ToString(); // Example provider header
        var body = await new StreamReader(req.Body).ReadToEndAsync();

        if (string.IsNullOrEmpty(eventId)) return new BadRequestResult();

        await webhookProcessor.ProcessAsync(provider, eventId, body);

        return new OkResult();
    }
}
