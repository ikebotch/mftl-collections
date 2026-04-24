using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Application.Features.Receipts.Queries.GetReceiptById;
using MFTL.Collections.Application.Features.Receipts.Queries.ListReceipts;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Api.Functions.Receipts;

public class ReceiptFunctions(IMediator mediator)
{
    [Function("GetReceiptById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Receipts.GetById)] HttpRequest req,
        Guid id)
    {
        var result = await mediator.Send(new GetReceiptByIdQuery(id));
        return new OkObjectResult(new ApiResponse<ReceiptDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("ListReceipts")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Receipts.Base)] HttpRequest req)
    {
        var result = await mediator.Send(new ListReceiptsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<ReceiptListItemDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
