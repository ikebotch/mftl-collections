using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Payments.Queries.ListPayments;

public record ListPaymentsQuery() : IRequest<IEnumerable<PaymentDto>>;

public class ListPaymentsQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListPaymentsQuery, IEnumerable<PaymentDto>>
{
    public async Task<IEnumerable<PaymentDto>> Handle(ListPaymentsQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Payments
            .Include(p => p.Receipt)
            .OrderByDescending(p => p.CreatedAt)
            .Select(payment => new PaymentDto(
                payment.Id,
                payment.ContributionId,
                payment.Receipt != null ? payment.Receipt.Id : null,
                payment.Amount,
                payment.Currency,
                payment.Method,
                payment.Status.ToString(),
                payment.ProviderReference,
                payment.CheckoutUrl,
                payment.CreatedAt,
                payment.ProcessedAt))
            .ToListAsync(cancellationToken);
    }
}
