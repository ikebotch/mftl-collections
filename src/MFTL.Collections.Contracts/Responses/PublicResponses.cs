namespace MFTL.Collections.Contracts.Responses;

public record PublicEventDto(
    Guid Id,
    string Title,
    string Slug,
    string Description,
    DateTimeOffset? EventDate);

public record PublicRecipientFundDto(
    Guid Id,
    string Name,
    string Description,
    decimal TargetAmount,
    decimal CollectedAmount);

public record PublicContributionDto(
    Guid Id,
    Guid EventId,
    Guid RecipientFundId,
    decimal Amount,
    string Currency,
    string ContributorName,
    string Status,
    string? Note);

public record PublicPaymentStatusDto(
    Guid PaymentId,
    string Status,
    string? ProviderReference);

public record PublicReceiptDto(
    Guid Id,
    string ReceiptNumber,
    string EventTitle,
    string RecipientFundName,
    string ContributorName,
    decimal Amount,
    string Currency,
    DateTimeOffset IssuedAt,
    string? Note);
