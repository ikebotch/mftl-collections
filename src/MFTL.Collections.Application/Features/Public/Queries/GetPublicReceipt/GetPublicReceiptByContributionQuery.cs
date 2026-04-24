using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Public.Queries.GetPublicReceipt;

public record GetPublicReceiptByContributionQuery(Guid ContributionId) : IRequest<PublicReceiptDto?>;

public class GetPublicReceiptByContributionQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetPublicReceiptByContributionQuery, PublicReceiptDto?>
{
    public async Task<PublicReceiptDto?> Handle(GetPublicReceiptByContributionQuery request, CancellationToken cancellationToken)
    {
        var receipt = await dbContext.Receipts
            .IgnoreQueryFilters()
            .Include(x => x.Event)
            .Include(x => x.RecipientFund)
            .Include(x => x.Contribution)
            .Where(x => x.ContributionId == request.ContributionId)
            .Select(x => new PublicReceiptDto(
                x.Id,
                x.ReceiptNumber,
                x.Event.Title,
                x.RecipientFund.Name,
                x.Contribution.ContributorName,
                x.Contribution.Amount,
                x.Contribution.Currency,
                x.IssuedAt,
                x.Note))
            .FirstOrDefaultAsync(cancellationToken);

        return receipt;
    }
}
