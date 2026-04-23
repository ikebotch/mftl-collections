using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Common.Interfaces;

public interface IPaymentStateService
{
    Task UpdatePaymentStatusAsync(Guid paymentId, PaymentStatus status, string? providerReference = null);
}
