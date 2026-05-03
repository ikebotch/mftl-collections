using MFTL.Collections.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Application.Common.Security;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MFTL.Collections.Application.Features.Payments.Commands.InitiateContributionPayment;

public record InitiateContributionPaymentCommand(Guid ContributionId, string Method, IDictionary<string, string>? Metadata = null) : IRequest<PaymentResult>;

public class InitiateContributionPaymentCommandHandler(
    IApplicationDbContext dbContext,
    IPaymentOrchestrator orchestrator,
    ICurrentUserService currentUserService,
    IScopeAccessService scopeService,
    ITenantContext tenantContext,
    ILogger<InitiateContributionPaymentCommandHandler> logger) : IRequestHandler<InitiateContributionPaymentCommand, PaymentResult>
{
    public async Task<PaymentResult> Handle(InitiateContributionPaymentCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Initiating payment: ContributionId={ContributionId}, Method={Method}, IsSystem={IsSystem}, IsPlatform={IsPlatform}, TenantId={TenantId}", 
            request.ContributionId, request.Method, tenantContext.IsSystemContext, tenantContext.IsPlatformContext, tenantContext.TenantId);

        // Load contribution first without unsafe projections or joins in the initial SQL
        var contribution = await dbContext.Contributions
            .FirstOrDefaultAsync(c => c.Id == request.ContributionId, cancellationToken);

        if (contribution == null)
        {
            logger.LogWarning("Contribution not found: {ContributionId}", request.ContributionId);
            return new PaymentResult(false, null, null, null, null, "Contribution not found");
        }

        // Explicitly load required context safely
        if (contribution.Event == null)
        {
            await dbContext.Events
                .Where(e => e.Id == contribution.EventId)
                .LoadAsync(cancellationToken);
        }

        if (contribution.ContributorId.HasValue && contribution.Contributor == null)
        {
            await dbContext.Contributors
                .Where(c => c.Id == contribution.ContributorId)
                .LoadAsync(cancellationToken);
        }

        // Only load payment if it exists
        if (contribution.PaymentId.HasValue)
        {
            await dbContext.Payments
                .Where(p => p.Id == contribution.PaymentId)
                .LoadAsync(cancellationToken);
        }

        // Validate required context fields after loading
        if (contribution.Event == null)
        {
            return new PaymentResult(false, null, null, null, null, "Contribution is missing event context.");
        }

        // Scope Enforcement for Authenticated Users (Collectors/Admins)
        var auth0Id = currentUserService.UserId;
        if (!string.IsNullOrEmpty(auth0Id))
        {
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

        // If an initiated/processing payment already exists, reuse it and ensure notification is queued
        if (contribution.Payment is { Status: PaymentStatus.Initiated or PaymentStatus.Processing } existingPayment)
        {
            if (!string.IsNullOrWhiteSpace(existingPayment.CheckoutUrl) && !string.IsNullOrWhiteSpace(contribution.Contributor?.Email))
            {
                var alreadyQueued = await dbContext.OutboxMessages
                    .IgnoreQueryFilters()
                    .AnyAsync(m => m.AggregateId == existingPayment.Id && m.EventType == "PaymentAuthorisationRequestedEvent", cancellationToken);

                if (!alreadyQueued)
                {
                    dbContext.OutboxMessages.Add(new OutboxMessage
                    {
                        TenantId = existingPayment.TenantId,
                        BranchId = contribution.BranchId,
                        AggregateId = existingPayment.Id,
                        AggregateType = "Payment",
                        EventType = "PaymentAuthorisationRequestedEvent",
                        Payload = JsonSerializer.Serialize(new { PaymentId = existingPayment.Id, ContributionId = contribution.Id, CheckoutUrl = existingPayment.CheckoutUrl }),
                        Status = OutboxMessageStatus.Pending,
                        CorrelationId = Guid.NewGuid().ToString()
                    });
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            return new PaymentResult(true, existingPayment.CheckoutUrl, existingPayment.ProviderReference, existingPayment.Id, existingPayment.Status.ToString());
        }

        // Initiate new payment
        logger.LogInformation("Orchestrating new payment initiation. ContributionId={ContributionId} Method={Method}", 
            contribution.Id, request.Method);
            
        var result = await orchestrator.InitiatePaymentAsync(contribution.Id, contribution.Amount, request.Method, request.Metadata, cancellationToken);
        
        if (!result.Success)
        {
            logger.LogWarning("Payment orchestrator failed to initiate payment. ContributionId={ContributionId} Error={Error}", 
                contribution.Id, result.Error);
            
            // Mark contribution as failed to prevent retry of malformed/rejected initiations
            contribution.Status = ContributionStatus.Failed;
            await dbContext.SaveChangesAsync(cancellationToken);
            
            return new PaymentResult(false, null, result.ProviderReference, null, null, result.Error);
        }

        logger.LogInformation("Payment orchestrator successfully initiated payment. ContributionId={ContributionId} ProviderRef={ProviderRef} CheckoutUrl={CheckoutUrl}", 
            contribution.Id, result.ProviderReference, result.RedirectUrl);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            ContributionId = contribution.Id,
            TenantId = contribution.TenantId,
            Amount = contribution.Amount,
            Currency = contribution.Currency,
            Method = request.Method,
            Status = PaymentStatus.Initiated,
            ProviderReference = result.ProviderReference,
            CheckoutUrl = result.RedirectUrl,
            ProcessedAt = null
        };

        dbContext.Payments.Add(payment);
        contribution.Status = ContributionStatus.AwaitingPayment;
        contribution.PaymentId = payment.Id;

        // Queue notification if we have a checkout URL and a customer email
        if (!string.IsNullOrWhiteSpace(result.RedirectUrl) && !string.IsNullOrWhiteSpace(contribution.Contributor?.Email))
        {
            logger.LogInformation("Queueing payment authorisation requested outbox message. PaymentId={PaymentId}", payment.Id);
            dbContext.OutboxMessages.Add(new OutboxMessage
            {
                TenantId = payment.TenantId,
                BranchId = contribution.BranchId,
                AggregateId = payment.Id,
                AggregateType = "Payment",
                EventType = "PaymentAuthorisationRequestedEvent",
                Payload = JsonSerializer.Serialize(new { PaymentId = payment.Id, ContributionId = contribution.Id, CheckoutUrl = payment.CheckoutUrl }),
                Status = OutboxMessageStatus.Pending,
                CorrelationId = Guid.NewGuid().ToString()
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Saved payment record and updated contribution status. PaymentId={PaymentId} ContributionId={ContributionId}", 
            payment.Id, contribution.Id);

        return new PaymentResult(true, payment.CheckoutUrl, payment.ProviderReference, payment.Id, payment.Status.ToString());
    }
}
