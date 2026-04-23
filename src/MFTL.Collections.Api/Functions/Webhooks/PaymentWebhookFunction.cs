using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;
using MFTL.Collections.Infrastructure.Payments;
using MFTL.Collections.Contracts.Common;

namespace MFTL.Collections.Api.Functions.Webhooks;

public class PaymentWebhookFunction(IPaymentWebhookProcessor webhookProcessor)
{
    [Function("PaymentWebhook")]
    [OpenApiOperation(operationId: "PaymentWebhook", tags: new[] { "Webhooks" })]
    [OpenApiParameter(name: "provider", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(string))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Payments.Webhook + "/{provider}")] HttpRequestData req, string provider)
    {
        var eventId = req.Headers.TryGetValues("X-Event-Id", out var values) ? values.FirstOrDefault() : null;
        var body = await new StreamReader(req.Body).ReadToEndAsync();

        if (string.IsNullOrEmpty(eventId))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        await webhookProcessor.ProcessAsync(provider, eventId, body);

        return req.CreateResponse(HttpStatusCode.OK);
    }
}
