using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Infrastructure.Services;

public class PaymentStateService(IApplicationDbContext dbContext) : IPaymentStateService
{
    public async Task UpdatePaymentStatusAsync(Guid paymentId, PaymentStatus status, string? providerReference = null)
    {
        var payment = await dbContext.Payments.FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment != null)
        {
            payment.Status = status;
            if (providerReference != null)
            {
                payment.ProviderReference = providerReference;
            }
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
    }
}
