using System.Text.Json.Serialization;

namespace MFTL.Collections.Contracts.Common;

public record ApiResponse<T>(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string? Message = null,
    [property: JsonPropertyName("data")] T? Data = default,
    [property: JsonPropertyName("errors")] IEnumerable<string>? Errors = null,
    [property: JsonPropertyName("correlationId")] string? CorrelationId = null);

public record ApiResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string? Message = null,
    [property: JsonPropertyName("errors")] IEnumerable<string>? Errors = null,
    [property: JsonPropertyName("correlationId")] string? CorrelationId = null);
