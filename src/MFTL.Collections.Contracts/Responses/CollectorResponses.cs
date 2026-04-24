namespace MFTL.Collections.Contracts.Responses;

public record CollectorDto(
    Guid Id,
    string Name,
    string Email,
    string? PhoneNumber,
    string Status,
    int AssignedEventCount,
    int AssignedFundCount,
    decimal TotalCollectedToday,
    decimal TotalCollectedMonth,
    DateTimeOffset? LastActiveAt);

public record CreateCollectorRequest(
    string Name,
    string Email,
    string? PhoneNumber,
    string? Type, // e.g. Staff, Volunteer
    string? Status,
    string? Notes,
    IEnumerable<Guid>? AssignedEventIds,
    IEnumerable<Guid>? AssignedFundIds);

public record CollectorMeDto(
    Guid Id,
    string Name,
    string Email,
    string Status,
    int AssignedEventCount,
    int AssignedFundCount,
    decimal TotalCollectedToday,
    int ReceiptsIssuedToday,
    DateTimeOffset? LastActiveAt);

public record CollectorAssignmentDto(
    Guid Id,
    string Title,
    string Location,
    string Date,
    int FundCount);
