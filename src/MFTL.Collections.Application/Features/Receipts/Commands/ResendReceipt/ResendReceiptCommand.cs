using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using Microsoft.Extensions.Logging;

namespace MFTL.Collections.Application.Features.Receipts.Commands.ResendReceipt;

[HasPermission("receipts.view")]
public record ResendReceiptCommand(Guid ReceiptId) : IRequest<bool>, IHasScope
{
    public Guid? GetScopeId() => null; // Scope will be checked in handler or we could resolve it
}

public class ResendReceiptCommandHandler(
    IApplicationDbContext dbContext,
    ILogger<ResendReceiptCommandHandler> logger) : IRequestHandler<ResendReceiptCommand, bool>
{
    public async Task<bool> Handle(ResendReceiptCommand request, CancellationToken cancellationToken)
    {
        var receipt = await dbContext.Receipts
            .Include(r => r.Contribution)
                .ThenInclude(c => c.Contributor)
            .Include(r => r.Contribution)
                .ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(r => r.Id == request.ReceiptId, cancellationToken);

        if (receipt?.Contribution == null) 
        {
            logger.LogWarning("Receipt {ReceiptId} has missing contribution details.", request.ReceiptId);
            return false;
        }

        var contribution = receipt.Contribution;
        var contributor = contribution.Contributor;
        var @event = contribution.Event;

        if (@event == null)
        {
            logger.LogWarning("Event for receipt {ReceiptId} not found.", request.ReceiptId);
            return false;
        }

        // Raise Domain Event for asynchronous processing
        receipt.AddDomainEvent(new Domain.Events.ReceiptResendRequestedEvent(
            receipt.Id,
            receipt.TenantId,
            receipt.BranchId,
            contribution.Id,
            receipt.ReceiptNumber,
            contribution.ContributorName,
            contributor?.Email,
            contributor?.PhoneNumber,
            contribution.Amount,
            contribution.Currency,
            @event.Title));

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
