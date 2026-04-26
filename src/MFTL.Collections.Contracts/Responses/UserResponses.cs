namespace MFTL.Collections.Contracts.Responses;

public record UserDto(
    Guid Id,
    string Name,
    string Email,
    string Role,
    string Status,
    string InviteState,
    string Scope);

public record UserDetailDto(
    Guid Id,
    string Auth0Id,
    string Email,
    string Name,
    string PhoneNumber,
    string Status,
    DateTimeOffset CreatedAt,
    IEnumerable<ScopeAssignmentDto> ScopeAssignments);

public record ScopeAssignmentDto(
    Guid Id,
    string Role,
    string ScopeType,
    Guid? TargetId);
