using MFTL.Collections.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Features.Payments.Commands.InitiateContributionPayment;

public record InitiateContributionPaymentCommand(Guid ContributionId, string Method) : IRequest<PaymentResult>;

public class InitiateContributionPaymentCommandHandler(
    IApplicationDbContext dbContext,
    IPaymentOrchestrator orchestrator) : IRequestHandler<InitiateContributionPaymentCommand, PaymentResult>
{
    public async Task<PaymentResult> Handle(InitiateContributionPaymentCommand request, CancellationToken cancellationToken)
    {
        var contribution = await dbContext.Contributions
            .Include(c => c.Payment)
            .FirstOrDefaultAsync(c => c.Id == request.ContributionId, cancellationToken);

        if (contribution == null)
        {
            return new PaymentResult(false, null, null, null, null, "Contribution not found");
        }

        if (contribution.Status == ContributionStatus.Completed)
        {
            return new PaymentResult(false, null, null, contribution.PaymentId, contribution.Status.ToString(), "Contribution is already settled.");
        }

        if (contribution.Payment is { Status: PaymentStatus.Pending or PaymentStatus.Initiated or PaymentStatus.Processing } existingPayment)
        {
            return new PaymentResult(true, null, existingPayment.ProviderReference, existingPayment.Id, existingPayment.Status.ToString());
        }

        var payment = new Payment
        {
            ContributionId = contribution.Id,
            TenantId = contribution.TenantId,
            Amount = contribution.Amount,
            Currency = contribution.Currency,
            Method = request.Method,
            Status = PaymentStatus.Pending,
        };

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = await orchestrator.InitiatePaymentAsync(contribution.Id, contribution.Amount, request.Method, cancellationToken);

        payment.ProviderReference = result.ProviderReference;
        payment.Status = result.Success ? PaymentStatus.Initiated : PaymentStatus.Failed;
        payment.ProcessedAt = result.Success ? null : DateTimeOffset.UtcNow;

        if (result.Success)
        {
            contribution.Status = ContributionStatus.AwaitingPayment;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return result with
        {
            PaymentId = payment.Id,
            Status = payment.Status.ToString(),
        };
    }
}
