namespace MFTL.Collections.Contracts.Requests;

public record CreateEventRequest(
    string Title,
    string Description,
    DateTimeOffset? EventDate);

public record EventDto(
    Guid Id,
    string Title,
    string Description,
    string Slug,
    DateTimeOffset? EventDate,
    bool IsActive);
