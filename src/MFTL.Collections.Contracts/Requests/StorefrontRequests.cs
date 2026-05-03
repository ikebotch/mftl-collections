namespace MFTL.Collections.Contracts.Requests;

public record CreateStorefrontContributionRequest(
    Guid RecipientFundId,
    decimal Amount,
    string Currency,
    string DonorName,
    string? DonorPhone,
    string? DonorEmail,
    bool Anonymous,
    string PaymentMethod,
    string? DonorNetwork,
    string? Note);

public record StorefrontContributionResponse(
    Guid ContributionId,
    Guid? PaymentId,
    string? ProviderReference,
    string Status,
    string PaymentMethod,
    string? CheckoutUrl,
    string? RedirectUrl);
