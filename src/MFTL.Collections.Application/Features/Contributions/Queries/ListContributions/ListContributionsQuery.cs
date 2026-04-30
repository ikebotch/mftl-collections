using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Application.Features.Contributions.Queries.ListContributions;

public sealed record ContributionListItemDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    string EventTitle,
    string RecipientFundName,
    string PaymentMethod,
    string Status,
    decimal Amount,
    string Currency,
    string ContributorName,
    string ContributorPhone,
    string? ContributorEmail,
    string? CollectorName,
    string? Note,
    Guid? ReceiptId);

public record ListContributionsQuery() : IRequest<IEnumerable<ContributionListItemDto>>;

public class ListContributionsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ListContributionsQuery, IEnumerable<ContributionListItemDto>>
{
    public async Task<IEnumerable<ContributionListItemDto>> Handle(
        ListContributionsQuery request,
        CancellationToken cancellationToken)
    {
        return await dbContext.Contributions
            .Include(c => c.Event)
            .Include(c => c.RecipientFund)
            .Include(c => c.Contributor)
            .Include(c => c.Receipt)
                .ThenInclude(r => r!.RecordedByUser)
            .OrderByDescending(c => c.CreatedAt)
            .Select(contribution => new ContributionListItemDto(
                contribution.Id,
                contribution.CreatedAt,
                contribution.Event == null ? "Unknown Event" : contribution.Event.Title,
                contribution.RecipientFund == null ? "General Fund" : contribution.RecipientFund.Name,
                contribution.Method,
                contribution.Status.ToString(),
                contribution.Amount,
                contribution.Currency,
                contribution.ContributorName,
                contribution.Contributor == null ? "" : (contribution.Contributor.PhoneNumber ?? ""),
                contribution.Contributor == null ? null : contribution.Contributor.Email,
                contribution.Receipt == null || contribution.Receipt.RecordedByUser == null ? null : contribution.Receipt.RecordedByUser.Name,
                contribution.Note,
                contribution.Receipt == null ? null : contribution.Receipt.Id))
            .ToListAsync(cancellationToken);
    }
}
