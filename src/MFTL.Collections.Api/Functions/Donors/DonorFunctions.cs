using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Features.Donors.Queries.ListDonors;

namespace MFTL.Collections.Api.Functions.Donors;

public class DonorFunctions(IMediator mediator)
{
    [Function("ListDonors")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Donors.Base)] HttpRequest req)
    {
        var result = await mediator.Send(new ListDonorsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<DonorDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
