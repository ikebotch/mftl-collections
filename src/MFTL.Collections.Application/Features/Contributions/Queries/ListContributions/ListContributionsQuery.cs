using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Common;

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

public record ListContributionsQuery(
    int Page = 1,
    int PageSize = 10,
    IEnumerable<Guid>? BranchIds = null,
    IEnumerable<Guid>? TenantIds = null) : IRequest<PagedResponse<ContributionListItemDto>>;

public class ListContributionsQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ListContributionsQuery, PagedResponse<ContributionListItemDto>>
{
    public async Task<PagedResponse<ContributionListItemDto>> Handle(
        ListContributionsQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Contributions
            .Include(c => c.Event)
            .Include(c => c.RecipientFund)
            .Include(c => c.Contributor)
            .Include(c => c.Receipt)
                .ThenInclude(r => r!.RecordedByUser)
            .AsQueryable();

        if (request.BranchIds != null && request.BranchIds.Any())
        {
            query = query.Where(c => request.BranchIds.Contains(c.BranchId));
        }

        if (request.TenantIds != null && request.TenantIds.Any())
        {
            query = query.Where(c => request.TenantIds.Contains(c.TenantId));
        }

        query = query.OrderByDescending(c => c.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(contribution => new ContributionListItemDto(
                contribution.Id,
                contribution.CreatedAt,
                contribution.Event != null ? contribution.Event.Title : "Unknown Event",
                contribution.RecipientFund != null ? contribution.RecipientFund.Name : "General Fund",
                contribution.Method,
                contribution.Status.ToString(),
                contribution.Amount,
                contribution.Currency,
                contribution.ContributorName,
                contribution.Contributor != null ? contribution.Contributor.PhoneNumber ?? "" : "",
                contribution.Contributor != null ? contribution.Contributor.Email : null,
                contribution.Receipt != null && contribution.Receipt.RecordedByUser != null ? contribution.Receipt.RecordedByUser.Name : null,
                contribution.Note,
                contribution.Receipt != null ? contribution.Receipt.Id : (Guid?)null))
            .ToListAsync(cancellationToken);

        return new PagedResponse<ContributionListItemDto>(items, totalCount, request.Page, request.PageSize);
    }
}
