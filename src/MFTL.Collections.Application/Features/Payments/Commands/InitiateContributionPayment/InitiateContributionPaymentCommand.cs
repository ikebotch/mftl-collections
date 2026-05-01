using MFTL.Collections.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Payments.Commands.InitiateContributionPayment;

public record InitiateContributionPaymentCommand(Guid ContributionId, string Method, IDictionary<string, string>? Metadata = null) : IRequest<PaymentResult>;

public class InitiateContributionPaymentCommandHandler(
    IApplicationDbContext dbContext,
    IPaymentOrchestrator orchestrator,
    ICurrentUserService currentUserService,
    IScopeAccessService scopeService) : IRequestHandler<InitiateContributionPaymentCommand, PaymentResult>
{
    public async Task<PaymentResult> Handle(InitiateContributionPaymentCommand request, CancellationToken cancellationToken)
    {
        var contribution = await dbContext.Contributions
            .Include(c => c.Payment)
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.Id == request.ContributionId, cancellationToken);

        if (contribution == null)
        {
            return new PaymentResult(false, null, null, null, null, "Contribution not found");
        }

        // Scope Enforcement for Authenticated Users (Collectors/Admins)
        var auth0Id = currentUserService.UserId;
        if (!string.IsNullOrEmpty(auth0Id))
        {
            // We check if they have the initiate permission for this specific event or fund.
            var hasAccess = await scopeService.CanAccessAsync(
                Permissions.Payments.Initiate,
                contribution.TenantId,
                contribution.Event?.BranchId,
                contribution.EventId,
                contribution.RecipientFundId,
                cancellationToken);

            if (!hasAccess)
            {
                return new PaymentResult(false, null, null, null, null, "You do not have permission to initiate payment for this contribution.");
            }
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
        Console.WriteLine($"[DEBUG] InitiateContributionPayment: Created Payment row {payment.Id} in Collections for Contribution {contribution.Id}");

        var result = await orchestrator.InitiatePaymentAsync(contribution.Id, contribution.Amount, request.Method, request.Metadata, cancellationToken);
        Console.WriteLine($"[DEBUG] InitiateContributionPayment: Orchestrator Result. Success={result.Success}, ProviderReference={result.ProviderReference}, Error={result.Error}");

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
