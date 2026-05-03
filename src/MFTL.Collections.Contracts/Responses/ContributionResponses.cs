namespace MFTL.Collections.Contracts.Responses;

public record CashContributionResult(
    Guid ContributionId,
    Guid? ReceiptId,
    string Status);

public record ContributionDto(
    Guid Id,
    Guid EventId,
    Guid RecipientFundId,
    decimal Amount,
    string Currency,
    string ContributorName,
    string Method,
    string Status,
    Guid? PaymentId,
    Guid? ReceiptId,
    string? Note);

public record ContributionStatusDto(
    Guid ContributionId,
    string Status,
    Guid? ReceiptId,
    string PaymentMethod,
    decimal Amount,
    string Currency);

public record StorefrontContributionStatusDto(
    Guid ContributionId,
    string Status,
    string? PaymentStatus,
    string? PaymentReference,
    string? FailureReason);
