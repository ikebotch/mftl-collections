using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Payments.Queries.GetPaymentById;

public record GetPaymentByIdQuery(Guid Id) : IRequest<PaymentDto>;

public class GetPaymentByIdQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetPaymentByIdQuery, PaymentDto>
{
    public async Task<PaymentDto> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .Include(p => p.Receipt)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (payment == null)
        {
            throw new KeyNotFoundException("Payment not found.");
        }

        return new PaymentDto(
            payment.Id,
            payment.ContributionId,
            payment.Receipt?.Id,
            payment.Amount,
            payment.Currency,
            payment.Method,
            payment.Status.ToString(),
            payment.ProviderReference,
            payment.CheckoutUrl,
            payment.CreatedAt,
            payment.ProcessedAt);
    }
}
