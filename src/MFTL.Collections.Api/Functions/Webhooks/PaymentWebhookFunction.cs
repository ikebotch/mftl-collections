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
        if (!configuration.GetValue<bool>("Payments:LegacyProviderWebhooks:Enabled"))
        {
            return new StatusCodeResult(StatusCodes.Status410Gone);
        }

        // NOTE: Webhooks are provider-to-system. They MUST NOT trust incoming tenant headers.
        // Isolation is enforced by resolving the payment via the provider's reference/payload.
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var paymentProvider = paymentProviders.FirstOrDefault(p => p.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase));

        if (paymentProvider == null)
        {
            return new BadRequestObjectResult($"Unsupported payment provider '{provider}'.");
        }

        var signature = req.Headers["Stripe-Signature"].FirstOrDefault()
            ?? req.Headers["x-paystack-signature"].FirstOrDefault()
            ?? req.Headers["X-Webhook-Signature"].FirstOrDefault();
        
        var secret = configuration[$"Payments:{provider}:WebhookSecret"]
            ?? configuration[$"Values:Payments:{provider}:WebhookSecret"];

        if (!paymentProvider.VerifySignature(body, signature ?? string.Empty, secret ?? string.Empty))
        {
            return new UnauthorizedObjectResult("Invalid webhook signature.");
        }

        // Parse the webhook to get the unique event ID for idempotency
        var parsed = paymentProvider.ParseWebhook(body);

        await webhookProcessor.ProcessAsync(provider, parsed.EventId, body);

        return new OkResult();
    }
}
