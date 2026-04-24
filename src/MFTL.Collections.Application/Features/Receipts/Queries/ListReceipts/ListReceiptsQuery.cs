using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Receipts.Queries.ListReceipts;

public record ListReceiptsQuery() : IRequest<IEnumerable<ReceiptListItemDto>>;

public class ListReceiptsQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListReceiptsQuery, IEnumerable<ReceiptListItemDto>>
{
    public async Task<IEnumerable<ReceiptListItemDto>> Handle(ListReceiptsQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Receipts
            .Include(r => r.Event)
            .Include(r => r.RecipientFund)
            .Include(r => r.Contribution)
            .Include(r => r.Payment)
            .OrderByDescending(r => r.IssuedAt)
            .Select(receipt => new ReceiptListItemDto(
                receipt.Id,
                receipt.ReceiptNumber,
                receipt.Status.ToString(),
                receipt.IssuedAt,
                receipt.Event.Title,
                receipt.RecipientFund.Name,
                receipt.Contribution.Amount,
                receipt.Contribution.Currency,
                receipt.Contribution.ContributorName,
                receipt.Contribution.Status.ToString(),
                receipt.Payment != null ? receipt.Payment.Status.ToString() : "Cash",
                receipt.Payment != null ? receipt.Payment.Method : receipt.Contribution.Method))
            .ToListAsync(cancellationToken);
    }
}
