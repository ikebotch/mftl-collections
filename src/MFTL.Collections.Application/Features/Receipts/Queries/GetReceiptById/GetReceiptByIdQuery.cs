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
            .Include(r => r.Branch)
            .Include(r => r.Event)
            .Include(r => r.RecipientFund)
            .Include(r => r.Contribution)
            .ThenInclude(c => c.Contributor)
            .Include(r => r.Payment)
            .Include(r => r.RecordedByUser)
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
            receipt.Branch!.TenantId,
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
            receipt.Contribution.Contributor?.PhoneNumber,
            receipt.Contribution.Contributor?.Email,
            receipt.Contribution.Contributor?.IsAnonymous ?? false,
            receipt.Contribution.Status.ToString(),
            receipt.Payment?.Status.ToString() ?? "Cash",
            receipt.Payment?.Method ?? receipt.Contribution.Method,
            receipt.RecordedByUser?.Name ?? "Collector",
            receipt.Note);
    }
}
