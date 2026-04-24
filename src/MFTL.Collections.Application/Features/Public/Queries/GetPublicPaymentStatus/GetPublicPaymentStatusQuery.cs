using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Public.Queries.GetPublicPaymentStatus;

public record GetPublicPaymentStatusQuery(Guid PaymentId) : IRequest<PublicPaymentStatusDto?>;

public class GetPublicPaymentStatusQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetPublicPaymentStatusQuery, PublicPaymentStatusDto?>
{
    public async Task<PublicPaymentStatusDto?> Handle(GetPublicPaymentStatusQuery request, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .IgnoreQueryFilters()
            .Where(x => x.Id == request.PaymentId)
            .Select(x => new PublicPaymentStatusDto(x.Id, x.Status.ToString(), x.ProviderReference))
            .FirstOrDefaultAsync(cancellationToken);

        return payment;
    }
}
