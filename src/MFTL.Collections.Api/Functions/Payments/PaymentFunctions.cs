using MFTL.Collections.Contracts.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Features.Payments.Commands.InitiateContributionPayment;
using MFTL.Collections.Application.Common.Interfaces;
using System.Text.Json;

namespace MFTL.Collections.Api.Functions.Payments;

public class PaymentFunctions(IMediator mediator)
{
    [Function("InitiateContributionPayment")]
    public async Task<IActionResult> Initiate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Payments.Initiate)] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<InitiatePaymentRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (request == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body."));

        var result = await mediator.Send(new InitiateContributionPaymentCommand(request.ContributionId, request.PaymentMethod));
        return new OkObjectResult(new ApiResponse<PaymentResult>(true, Data: result));
    }
}

public record InitiatePaymentRequest(Guid ContributionId, string PaymentMethod);
