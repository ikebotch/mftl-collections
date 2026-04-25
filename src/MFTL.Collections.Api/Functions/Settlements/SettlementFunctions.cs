using MediatR;
using MFTL.Collections.Application.Features.Settlements.Queries.ListSettlements;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace MFTL.Collections.Api.Functions.Settlements;

public class SettlementFunctions(IMediator mediator)
{
    [Function("ListSettlements")]
    public async Task<IActionResult> ListSettlements(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/settlements")] HttpRequest req)
    {
        var result = await mediator.Send(new ListSettlementsQuery());
        return new OkObjectResult(result);
    }
}
