namespace MFTL.Collections.Contracts.Responses;

public record PaymentResult(bool Success, string? RedirectUrl, string? ProviderReference, string? Error = null);
