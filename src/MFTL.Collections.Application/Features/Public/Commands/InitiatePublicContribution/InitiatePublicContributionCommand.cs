using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Contracts.Responses;
using System.Text.Json;

namespace MFTL.Collections.Application.Features.Public.Commands.InitiatePublicContribution;

public record InitiatePublicContributionCommand(
    string EventSlug,
    Guid RecipientFundId,
    decimal Amount,
    string Currency,
    string ContributorName,
    string? ContributorEmail,
    string? ContributorPhone,
    bool IsAnonymous,
    string Method,
    string? Note) : IRequest<PaymentResult>;

public class InitiatePublicContributionCommandHandler(
    IApplicationDbContext dbContext,
    ITenantContext tenantContext,
    IPaymentOrchestrator orchestrator) : IRequestHandler<InitiatePublicContributionCommand, PaymentResult>
{
    public async Task<PaymentResult> Handle(InitiatePublicContributionCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve Event and Tenant
        var @event = await dbContext.Events
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Slug == request.EventSlug, cancellationToken);

        if (@event == null)
        {
            return new PaymentResult(false, null, null, null, null, "Event not found");
        }

        // 2. Set Tenant Context to allow saving entities
        tenantContext.UseTenant(@event.TenantId);

        // 3. Create or find Contributor
        Contributor? contributor = null;
        if (!string.IsNullOrWhiteSpace(request.ContributorEmail))
        {
            contributor = await dbContext.Contributors
                .FirstOrDefaultAsync(x => x.Email == request.ContributorEmail, cancellationToken);
        }

        if (contributor == null && !string.IsNullOrWhiteSpace(request.ContributorName))
        {
            contributor = new Contributor
            {
                Name = request.ContributorName,
                Email = request.ContributorEmail ?? "",
                PhoneNumber = request.ContributorPhone,
                IsAnonymous = request.IsAnonymous
            };
            dbContext.Contributors.Add(contributor);
        }

        // 4. Create Contribution
        var contribution = new Contribution
        {
            EventId = @event.Id,
            RecipientFundId = request.RecipientFundId,
            ContributorId = contributor?.Id,
            Amount = request.Amount,
            Currency = request.Currency,
            ContributorName = request.ContributorName,
            Method = request.Method,
            Status = ContributionStatus.Pending,
            Note = request.Note,
            Reference = $"CONT-{Guid.NewGuid():N}".ToUpperInvariant()[..18]
        };

        dbContext.Contributions.Add(contribution);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 5. Create Payment
        var payment = new Payment
        {
            TenantId = @event.TenantId,
            ContributionId = contribution.Id,
            Amount = contribution.Amount,
            Currency = contribution.Currency,
            Method = request.Method,
            Status = PaymentStatus.Pending,
        };

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 6. Initiate Payment via Orchestrator
        var result = await orchestrator.InitiatePaymentAsync(contribution.Id, contribution.Amount, request.Method, cancellationToken);

        payment.ProviderReference = result.ProviderReference;
        payment.Status = result.Success ? PaymentStatus.Initiated : PaymentStatus.Failed;
        payment.ProcessedAt = result.Success ? null : DateTimeOffset.UtcNow;
        payment.ProviderPayload = result.Metadata != null ? JsonSerializer.Serialize(result.Metadata) : null;

        if (result.Success)
        {
            contribution.Status = ContributionStatus.AwaitingPayment;
            contribution.PaymentId = payment.Id;
        }
        else
        {
            contribution.Status = ContributionStatus.Failed;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return result with
        {
            PaymentId = payment.Id,
            Status = payment.Status.ToString()
        };
    }
}
