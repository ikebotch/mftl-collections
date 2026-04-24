using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Application.Features.Collectors.Queries.ListCollectorHistory;

public sealed record CollectorHistoryReceiptDto(
    Guid Id,
    string ReceiptNumber,
    string Status,
    DateTimeOffset IssuedAt,
    string EventTitle,
    string RecipientFundName,
    decimal Amount,
    string Currency,
    string ContributorName,
    string ContributionStatus,
    string PaymentStatus,
    string PaymentMethod);

public record ListCollectorHistoryQuery(string? ExplicitUserId = null) : IRequest<IEnumerable<CollectorHistoryReceiptDto>>;

public class ListCollectorHistoryQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<ListCollectorHistoryQuery, IEnumerable<CollectorHistoryReceiptDto>>
{
    public async Task<IEnumerable<CollectorHistoryReceiptDto>> Handle(
        ListCollectorHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var auth0Id = request.ExplicitUserId ?? currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(auth0Id))
        {
            throw new UnauthorizedAccessException("Collector authentication is required.");
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, cancellationToken);

        if (user == null && !string.IsNullOrWhiteSpace(request.ExplicitUserId))
        {
            user = await dbContext.Users
                .Where(u => u.IsActive && u.ScopeAssignments.Any(a => a.Role == "Collector"))
                .OrderBy(u => u.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (user == null)
        {
            throw new KeyNotFoundException("Collector profile not found.");
        }

        return await dbContext.Receipts
            .Where(r => r.RecordedByUserId == user.Id)
            .Include(r => r.Event)
            .Include(r => r.RecipientFund)
            .Include(r => r.Contribution)
            .Include(r => r.Payment)
            .OrderByDescending(r => r.IssuedAt)
            .Select(receipt => new CollectorHistoryReceiptDto(
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
