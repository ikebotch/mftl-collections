using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Features.Receipts.Queries.GetReceiptById;
using MFTL.Collections.Application.Features.Receipts.Queries.ListReceipts;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Api.Functions.Receipts;

public class ReceiptFunctions(
    IMediator mediator,
    IApplicationDbContext dbContext,
    IOutboxService outboxService,
    IScopeAccessService scopeService,
    ITenantContext tenantContext)
{
    [Function("GetReceiptById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Receipts.GetById)] HttpRequest req,
        Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Receipts.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new GetReceiptByIdQuery(id));
        return new OkObjectResult(new ApiResponse<ReceiptDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("ListReceipts")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Receipts.Base)] HttpRequest req)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Receipts.View, req);
        if (deny != null) return deny;

        var result = await mediator.Send(new ListReceiptsQuery());
        return new OkObjectResult(new ApiResponse<IEnumerable<ReceiptListItemDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("ResendReceipt")]
    public async Task<IActionResult> Resend(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Receipts.Resend)] HttpRequest req,
        Guid id)
    {
        var deny = await scopeService.RequirePermissionAsync(tenantContext, Permissions.Receipts.Resend, req);
        if (deny != null) return deny;

        var receipt = await dbContext.Receipts
            .Include(r => r.Contribution)
                .ThenInclude(c => c.Contributor)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (receipt == null)
        {
            return new NotFoundObjectResult(new ApiResponse(false, "Receipt not found.", CorrelationId: req.GetOrCreateCorrelationId()));
        }

        var outboxId = await outboxService.EnqueueAsync(
            receipt.TenantId,
            receipt.BranchId,
            receipt.Id,
            nameof(Domain.Entities.Receipt),
            "ReceiptResendRequestedEvent",
            new
            {
                ReceiptId = receipt.Id,
                TemplateKey = "receipt.resend"
            },
            correlationId: req.GetOrCreateCorrelationId());

        return new OkObjectResult(new ApiResponse<Guid>(true, "Receipt resend queued.", outboxId, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
