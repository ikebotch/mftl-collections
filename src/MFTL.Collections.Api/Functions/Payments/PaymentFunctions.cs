using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using MediatR;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Features.Payments.Commands.InitiateContributionPayment;
using Newtonsoft.Json;

namespace MFTL.Collections.Api.Functions.Payments;

public class PaymentFunctions(IMediator mediator)
{
    [Function("InitiateContributionPayment")]
    [OpenApiOperation(operationId: "InitiateContributionPayment", tags: new[] { "Payments" })]
    [OpenApiSecurity("Authorization", SecuritySchemeType.Http, Scheme = OpenApiSecuritySchemeType.Bearer, BearerFormat = "JWT")]
    [OpenApiParameter(name: "X-Tenant-Id", In = ParameterLocation.Header, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody("application/json", typeof(InitiatePaymentRequest))]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(ApiResponse<PaymentResult>))]
    public async Task<HttpResponseData> Initiate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Payments.Initiate)] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonConvert.DeserializeObject<InitiatePaymentRequest>(body);
        
        if (request == null)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteAsJsonAsync(new ApiResponse(false, "Invalid body."));
            return errorResponse;
        }

        var result = await mediator.Send(new InitiateContributionPaymentCommand(request.ContributionId, request.PaymentMethod));
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new ApiResponse<PaymentResult>(true, Data: result));
        return response;
    }
}

public record InitiatePaymentRequest(Guid ContributionId, string PaymentMethod);
