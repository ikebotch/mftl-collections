using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Application.Features.Payments.Commands.InitiateContributionPayment;
using Microsoft.Extensions.Logging;

namespace MFTL.Collections.Application.Features.Storefront.Commands.CreateStorefrontContribution;

public record CreateStorefrontContributionCommand(
    string Slug,
    string RecipientFundId,
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
    IMediator mediator,
    ILogger<CreateStorefrontContributionCommandHandler> logger) : IRequestHandler<CreateStorefrontContributionCommand, StorefrontContributionResponse>
{
    public async Task<StorefrontContributionResponse> Handle(CreateStorefrontContributionCommand request, CancellationToken cancellationToken)
    {
        // 1. Reject Cash
        if (request.PaymentMethod.Equals("cash", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cash payments are not available on the public storefront.");
        }

        // 1a. Validate Currency/Method Combination
        var isGhs = request.Currency.Equals("GHS", StringComparison.OrdinalIgnoreCase);
        var isMomo = request.PaymentMethod.Equals("momo", StringComparison.OrdinalIgnoreCase);
        var isBank = request.PaymentMethod.Equals("bank", StringComparison.OrdinalIgnoreCase);

        if (isGhs)
        {
            if (isBank)
            {
                throw new InvalidOperationException("Bank payment is available for GBP and EUR.");
            }
        }
        else
        {
            if (isMomo)
            {
                throw new InvalidOperationException("Mobile Money is available for GHS.");
            }
        }

        // 1b. Require Phone/Network for MoMo
        string? normalizedPhone = null;
        if (isMomo)
        {
            if (string.IsNullOrWhiteSpace(request.DonorPhone))
            {
                throw new InvalidOperationException("Donor phone number is required for Mobile Money payments.");
            }
            if (string.IsNullOrWhiteSpace(request.DonorNetwork))
            {
                throw new InvalidOperationException("Donor network is required for Mobile Money payments.");
            }

            normalizedPhone = MFTL.Collections.Application.Common.Utils.GhanaPhoneNormalizer.Normalize(request.DonorPhone);
            if (!MFTL.Collections.Application.Common.Utils.GhanaPhoneNormalizer.IsValid(normalizedPhone))
            {
                 throw new InvalidOperationException("Invalid Ghana phone number format. Please use a valid 10-digit number (e.g. 0244123456).");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.DonorPhone))
        {
            normalizedPhone = MFTL.Collections.Application.Common.Utils.GhanaPhoneNormalizer.Normalize(request.DonorPhone);
        }

        // 2. Resolve Event/Tenant/Branch from Slug
        logger.LogInformation("Resolving event for storefront contribution. Slug={Slug}", request.Slug);
        var ev = await dbContext.Events
            .IgnoreQueryFilters()
            .Include(e => e.RecipientFunds)
            .FirstOrDefaultAsync(e => e.Slug == request.Slug && e.IsActive, cancellationToken);

        if (ev == null)
        {
            logger.LogWarning("Event not found or inactive for storefront contribution. Slug={Slug}", request.Slug);
            throw new KeyNotFoundException($"Event with slug '{request.Slug}' not found or is not public.");
        }

        logger.LogInformation("Resolved event for storefront contribution. EventId={EventId} TenantId={TenantId} BranchId={BranchId}", 
            ev.Id, ev.TenantId, ev.BranchId);

        // 3. Validate Recipient Fund
        if (!Guid.TryParse(request.RecipientFundId, out var fundId))
        {
            throw new InvalidOperationException("Select a valid fund.");
        }

        var fund = ev.RecipientFunds.FirstOrDefault(f => f.Id == fundId && f.IsActive);
        if (fund == null)
        {
            logger.LogWarning("Recipient fund not found or inactive for storefront contribution. FundId={FundId} EventId={EventId}", 
                request.RecipientFundId, ev.Id);
            throw new InvalidOperationException("The specified recipient fund is not available for this event.");
        }

        // 4. Create or Resolve Donor/Contributor
        logger.LogInformation("Resolving contributor for storefront contribution. Email={Email} Phone={Phone}", 
            request.DonorEmail ?? "(none)", normalizedPhone ?? "(none)");
        var contributor = await ResolveContributorAsync(ev.TenantId, ev.BranchId, request, normalizedPhone, cancellationToken);

        // 5. Create Contribution (AwaitingPayment)
        var contribution = new Contribution
        {
            Id = Guid.NewGuid(),
            TenantId = ev.TenantId,
            BranchId = ev.BranchId,
            EventId = ev.Id,
            RecipientFundId = fund.Id,
            ContributorId = contributor.Id,
            ContributorName = request.Anonymous ? "Anonymous" : (request.DonorName ?? "Unknown"),
            Amount = request.Amount,
            Currency = request.Currency,
            Status = ContributionStatus.Pending, // Initial state
            Method = request.PaymentMethod,
            Note = request.Note,
            Reference = GenerateReference()
        };

        dbContext.Contributions.Add(contribution);
        
        // Elevate context to system to allow anonymous persistence of cross-tenant entities
        logger.LogInformation("Elevating context to system for storefront contribution persistence.");
        tenantContext.SetSystemContext();
        
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Saved initial contribution record. ContributionId={ContributionId} Reference={Reference}", 
            contribution.Id, contribution.Reference);

        // 6. Build Metadata for Payment Initiation
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(request.DonorNetwork))
        {
            metadata["momoNetwork"] = request.DonorNetwork;
            metadata["network"] = request.DonorNetwork; 
        }
        if (!string.IsNullOrWhiteSpace(normalizedPhone))
        {
            metadata["momoPhoneNumber"] = normalizedPhone;
            metadata["phoneNumber"] = normalizedPhone;
        }

        // 7. Initiate Payment using shared backend command
        logger.LogInformation("Initiating payment for storefront contribution. ContributionId={ContributionId} Method={Method}", 
            contribution.Id, request.PaymentMethod);
            
        var paymentResult = await mediator.Send(new InitiateContributionPaymentCommand(contribution.Id, request.PaymentMethod, metadata), cancellationToken);

        if (!paymentResult.Success)
        {
            logger.LogError("Payment initiation failed for storefront contribution. ContributionId={ContributionId} Error={Error}", 
                contribution.Id, paymentResult.Error);
            throw new InvalidOperationException(paymentResult.Error ?? "Payment initiation failed. Please try again.");
        }

        logger.LogInformation("Payment initiated successfully for storefront contribution. ContributionId={ContributionId} PaymentId={PaymentId} ProviderRef={ProviderRef}", 
            contribution.Id, paymentResult.PaymentId, paymentResult.ProviderReference);

        return new StorefrontContributionResponse(
            contribution.Id,
            paymentResult.PaymentId,
            paymentResult.ProviderReference,
            paymentResult.Status ?? ContributionStatus.AwaitingPayment.ToString(),
            request.PaymentMethod,
            paymentResult.RedirectUrl,
            paymentResult.RedirectUrl);
    }

    private async Task<Contributor> ResolveContributorAsync(Guid tenantId, Guid branchId, CreateStorefrontContributionCommand request, string? normalizedPhone, CancellationToken cancellationToken)
    {
        Contributor? contributor = null;
        if (!string.IsNullOrWhiteSpace(request.DonorEmail))
        {
            contributor = await dbContext.Contributors
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Email == request.DonorEmail, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(normalizedPhone))
        {
            contributor = await dbContext.Contributors
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.PhoneNumber == normalizedPhone, cancellationToken);
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
                PhoneNumber = normalizedPhone,
                IsAnonymous = request.Anonymous
            };
            dbContext.Contributors.Add(contributor);
        }

        return contributor;
    }

    private static string GenerateReference() => $"CONT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
