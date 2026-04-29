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
    ISmsService smsService,
    ISmsTemplateService templateService,
    ILogger<ResendReceiptCommandHandler> logger) : IRequestHandler<ResendReceiptCommand, bool>
{
    public async Task<bool> Handle(ResendReceiptCommand request, CancellationToken cancellationToken)
    {
        var receipt = await dbContext.Receipts
            .Include(r => r.Contribution)
                .ThenInclude(c => c.Contributor)
            .Include(r => r.Contribution)
                .ThenInclude(c => c.Event)
                    .ThenInclude(e => e.SmsTemplate)
            .FirstOrDefaultAsync(r => r.Id == request.ReceiptId, cancellationToken);

        if (receipt?.Contribution == null || receipt.Contribution.Contributor == null) 
        {
            logger.LogWarning("Receipt {ReceiptId} has missing contribution or contributor details.", request.ReceiptId);
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

        if (string.IsNullOrWhiteSpace(contributor.PhoneNumber))
        {
            throw new InvalidOperationException("Contributor does not have a phone number.");
        }

        var template = @event.SmsTemplate?.Content;
        if (string.IsNullOrEmpty(template))
        {
            template = "Thank you {ContributorName} for your contribution of {Currency} {Amount} to {EventTitle}. Receipt: {ReceiptNumber}";
        }

        var messageData = new
        {
            ContributorName = contribution.ContributorName,
            Amount = contribution.Amount,
            Currency = contribution.Currency,
            EventTitle = @event.Title,
            ReceiptNumber = receipt.ReceiptNumber
        };

        var message = templateService.Render(template, messageData);
        await smsService.SendSmsAsync(contributor.PhoneNumber, message);

        return true;
    }
}
