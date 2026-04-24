using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Collectors.Queries.ListCollectorHistory;

public record ListCollectorHistoryQuery() : IRequest<IEnumerable<ReceiptListItemDto>>;

public class ListCollectorHistoryQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<ListCollectorHistoryQuery, IEnumerable<ReceiptListItemDto>>
{
    public async Task<IEnumerable<ReceiptListItemDto>> Handle(ListCollectorHistoryQuery request, CancellationToken cancellationToken)
    {
        var auth0Id = currentUserService.UserId;
        if (string.IsNullOrEmpty(auth0Id))
        {
            return Enumerable.Empty<ReceiptListItemDto>();
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, cancellationToken);

        if (user == null)
        {
            return Enumerable.Empty<ReceiptListItemDto>();
        }

        return await dbContext.Receipts
            .Where(r => r.RecordedByUserId == user.Id)
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
