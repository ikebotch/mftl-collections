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

public record ContributionListItemDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    string EventTitle,
    string RecipientFundName,
    string PaymentMethod,
    string Status,
    decimal Amount,
    string Currency);
