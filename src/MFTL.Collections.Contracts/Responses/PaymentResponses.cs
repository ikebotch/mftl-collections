namespace MFTL.Collections.Contracts.Responses;

public record PaymentResult(
    bool Success,
    string? RedirectUrl,
    string? ProviderReference,
    Guid? PaymentId = null,
    string? Status = null,
    string? Error = null);

public record PaymentDto(
    Guid Id,
    Guid ContributionId,
    Guid? ReceiptId,
    decimal Amount,
    string Currency,
    string Method,
    string Status,
    string? ProviderReference,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt);
