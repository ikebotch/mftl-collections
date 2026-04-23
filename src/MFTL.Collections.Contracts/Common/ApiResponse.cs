namespace MFTL.Collections.Contracts.Common;

public record ApiResponse<T>(
    bool Success,
    string? Message = null,
    T? Data = default,
    IEnumerable<string>? Errors = null,
    string? CorrelationId = null);

public record ApiResponse(
    bool Success,
    string? Message = null,
    IEnumerable<string>? Errors = null,
    string? CorrelationId = null);
