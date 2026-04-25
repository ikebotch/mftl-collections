using MFTL.Collections.Contracts.Common;

namespace MFTL.Collections.Contracts.Requests;

public record CreateEventRequest(
    string Title,
    string Description,
    DateTimeOffset? EventDate,
    string? Slug = null,
    string? DisplayImageUrl = null,
    string? ReceiptLogoUrl = null);

public record UpdateEventRequest(
    string Title,
    string Description,
    DateTimeOffset? EventDate,
    bool IsActive,
    string? Slug = null,
    string? DisplayImageUrl = null,
    string? ReceiptLogoUrl = null);

public record EventDto(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset? EventDate,
    bool IsActive,
    IEnumerable<CurrencyTotalDto> Totals,
    decimal TotalTarget = 0,
    int FundCount = 0,
    int CollectorCount = 0,
    string? Slug = null,
    string? DisplayImageUrl = null,
    string? ReceiptLogoUrl = null);
