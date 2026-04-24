namespace MFTL.Collections.Contracts.Requests;

public record CreateEventRequest(
    string Title,
    string Description,
    DateTimeOffset? EventDate);

public record EventDto(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset? EventDate,
    bool IsActive,
    decimal TotalRaised = 0,
    decimal TotalTarget = 0,
    int FundCount = 0,
    int CollectorCount = 0,
    string? Slug = null);
