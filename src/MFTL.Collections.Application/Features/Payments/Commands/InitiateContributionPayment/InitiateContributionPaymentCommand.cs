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
            .FirstOrDefaultAsync(c => c.Id == request.ContributionId, cancellationToken);

        if (contribution == null)
        {
            return new PaymentResult(false, null, null, "Contribution not found");
        }

        // Use the orchestrator to call the provider
        var result = await orchestrator.InitiatePaymentAsync(contribution.Id, contribution.Amount, request.Method, cancellationToken);

        if (result.Success)
        {
            contribution.Status = ContributionStatus.AwaitingPayment;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}
