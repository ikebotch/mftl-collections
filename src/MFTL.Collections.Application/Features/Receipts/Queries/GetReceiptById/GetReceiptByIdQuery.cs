using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Receipts.Queries.GetReceiptById;

public record GetReceiptByIdQuery(Guid Id) : IRequest<ReceiptDto>;

public class GetReceiptByIdQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetReceiptByIdQuery, ReceiptDto>
{
    public async Task<ReceiptDto> Handle(GetReceiptByIdQuery request, CancellationToken cancellationToken)
    {
        var receipt = await dbContext.Receipts
            .Include(r => r.Event)
            .Include(r => r.RecipientFund)
            .Include(r => r.Contribution)
            .Include(r => r.Payment)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (receipt == null)
        {
            throw new KeyNotFoundException("Receipt not found.");
        }

        return new ReceiptDto(
            receipt.Id,
            receipt.ReceiptNumber,
            receipt.Status.ToString(),
            receipt.IssuedAt,
            receipt.TenantId,
            receipt.EventId,
            receipt.Event.Title,
            receipt.RecipientFundId,
            receipt.RecipientFund.Name,
            receipt.ContributionId,
            receipt.PaymentId,
            receipt.RecordedByUserId,
            receipt.Contribution.Amount,
            receipt.Contribution.Currency,
            receipt.Contribution.ContributorName,
            receipt.Contribution.Status.ToString(),
            receipt.Payment?.Status.ToString() ?? "Cash",
            receipt.Payment?.Method ?? receipt.Contribution.Method,
            receipt.Note);
    }
}
