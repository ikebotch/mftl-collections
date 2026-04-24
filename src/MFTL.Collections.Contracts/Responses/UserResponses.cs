namespace MFTL.Collections.Contracts.Responses;

public record UserDto(
    Guid Id,
    string Name,
    string Email,
    string Role,
    string Status,
    string InviteState,
    string Scope);
