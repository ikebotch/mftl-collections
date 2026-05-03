using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Application.Features.Payments.Commands.InitiateContributionPayment;

namespace MFTL.Collections.Application.Features.Storefront.Commands.CreateStorefrontContribution;

public record CreateStorefrontContributionCommand(
    string Slug,
    Guid RecipientFundId,
    decimal Amount,
    string Currency,
    string DonorName,
    string? DonorPhone,
    string? DonorEmail,
    bool Anonymous,
    string PaymentMethod,
    string? DonorNetwork,
    string? Note) : IRequest<StorefrontContributionResponse>;

public class CreateStorefrontContributionCommandHandler(
    IApplicationDbContext dbContext,
    ITenantContext tenantContext,
    IMediator mediator) : IRequestHandler<CreateStorefrontContributionCommand, StorefrontContributionResponse>
{
    public async Task<StorefrontContributionResponse> Handle(CreateStorefrontContributionCommand request, CancellationToken cancellationToken)
    {
        // 1. Reject Cash
        if (request.PaymentMethod.Equals("cash", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cash payments are not available on the public storefront.");
        }

        // 1b. Require Phone/Network for MoMo
        if (request.PaymentMethod.Equals("momo", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.DonorPhone))
            {
                throw new InvalidOperationException("Donor phone number is required for Mobile Money payments.");
            }
            if (string.IsNullOrWhiteSpace(request.DonorNetwork))
            {
                throw new InvalidOperationException("Donor network is required for Mobile Money payments.");
            }
        }

        // 2. Resolve Event/Tenant/Branch from Slug
        var ev = await dbContext.Events
            .IgnoreQueryFilters()
            .Include(e => e.RecipientFunds)
            .FirstOrDefaultAsync(e => e.Slug == request.Slug && e.IsActive, cancellationToken);

        if (ev == null)
        {
            throw new KeyNotFoundException($"Event with slug '{request.Slug}' not found or is not public.");
        }

        // 3. Validate Recipient Fund
        var fund = ev.RecipientFunds.FirstOrDefault(f => f.Id == request.RecipientFundId && f.IsActive);
        if (fund == null)
        {
            throw new InvalidOperationException("The specified recipient fund is not available for this event.");
        }

        // 4. Create or Resolve Donor/Contributor
        var contributor = await ResolveContributorAsync(ev.TenantId, ev.BranchId, request, cancellationToken);

        // 5. Create Contribution (AwaitingPayment)
        var contribution = new Contribution
        {
            Id = Guid.NewGuid(),
            TenantId = ev.TenantId,
            BranchId = ev.BranchId,
            EventId = ev.Id,
            RecipientFundId = fund.Id,
            ContributorId = contributor.Id,
            ContributorName = request.Anonymous ? "Anonymous" : request.DonorName,
            Amount = request.Amount,
            Currency = request.Currency,
            Status = ContributionStatus.Pending, // Initial state
            Method = request.PaymentMethod,
            Note = request.Note,
            Reference = GenerateReference()
        };

        dbContext.Contributions.Add(contribution);
        
        // Elevate context to system to allow anonymous persistence of cross-tenant entities
        tenantContext.SetSystemContext();
        
        await dbContext.SaveChangesAsync(cancellationToken);

        // 6. Build Metadata for Payment Initiation
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(request.DonorNetwork))
        {
            metadata["momoNetwork"] = request.DonorNetwork;
            metadata["network"] = request.DonorNetwork; // Some providers might use this
        }
        if (!string.IsNullOrWhiteSpace(request.DonorPhone))
        {
            metadata["momoPhoneNumber"] = request.DonorPhone;
        }

        // 7. Initiate Payment using shared backend command
        var paymentResult = await mediator.Send(new InitiateContributionPaymentCommand(contribution.Id, request.PaymentMethod, metadata), cancellationToken);

        if (!paymentResult.Success)
        {
            throw new InvalidOperationException(paymentResult.Error ?? "Payment initiation failed.");
        }

        return new StorefrontContributionResponse(
            contribution.Id,
            paymentResult.PaymentId,
            paymentResult.ProviderReference,
            paymentResult.Status ?? ContributionStatus.AwaitingPayment.ToString(),
            request.PaymentMethod,
            paymentResult.RedirectUrl,
            paymentResult.RedirectUrl);
    }

    private async Task<Contributor> ResolveContributorAsync(Guid tenantId, Guid branchId, CreateStorefrontContributionCommand request, CancellationToken cancellationToken)
    {
        Contributor? contributor = null;
        if (!string.IsNullOrWhiteSpace(request.DonorEmail))
        {
            contributor = await dbContext.Contributors
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Email == request.DonorEmail, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.DonorPhone))
        {
            contributor = await dbContext.Contributors
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.PhoneNumber == request.DonorPhone, cancellationToken);
        }

        if (contributor == null)
        {
            contributor = new Contributor
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = branchId,
                Name = request.DonorName,
                Email = request.DonorEmail ?? string.Empty,
                PhoneNumber = request.DonorPhone,
                IsAnonymous = request.Anonymous
            };
            dbContext.Contributors.Add(contributor);
        }

        return contributor;
    }

    private static string GenerateReference() => $"CONT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
