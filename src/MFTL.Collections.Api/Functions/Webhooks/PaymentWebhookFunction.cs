using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MFTL.Collections.Infrastructure.Payments;
using MFTL.Collections.Contracts.Common;
using Microsoft.Extensions.Configuration;

namespace MFTL.Collections.Api.Functions.Webhooks;

public class PaymentWebhookFunction(
    IPaymentWebhookProcessor webhookProcessor,
    IEnumerable<IPaymentProvider> paymentProviders,
    IConfiguration configuration)
{
    [Function("PaymentWebhook")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Payments.Webhook + "/{provider}")] HttpRequest req, string provider)
    {
        var eventId = req.Headers["X-Event-Id"].ToString();
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var paymentProvider = paymentProviders.FirstOrDefault(p => p.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase));

        if (paymentProvider == null)
        {
            return new BadRequestObjectResult($"Unsupported payment provider '{provider}'.");
        }

        if (string.IsNullOrEmpty(eventId))
        {
            return new BadRequestObjectResult("Missing webhook event id.");
        }

        var signature = req.Headers["Stripe-Signature"].FirstOrDefault()
            ?? req.Headers["x-paystack-signature"].FirstOrDefault()
            ?? req.Headers["X-Webhook-Signature"].FirstOrDefault();
        var secret = configuration[$"Values:Payments:{provider}:WebhookSecret"]
            ?? configuration[$"Payments:{provider}:WebhookSecret"]
            ?? configuration[$"Values:{provider}:WebhookSecret"]
            ?? configuration[$"{provider}:WebhookSecret"];

        if (!paymentProvider.VerifySignature(body, signature ?? string.Empty, secret ?? string.Empty))
        {
            return new UnauthorizedObjectResult("Invalid webhook signature.");
        }

        await webhookProcessor.ProcessAsync(provider, eventId, body);

        return new OkResult();
    }
}
