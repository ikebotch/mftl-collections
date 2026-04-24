namespace MFTL.Collections.Contracts.Responses;

public record DonorDto(
    string Name,
    string? Email,
    string? PhoneNumber,
    decimal TotalGiven,
    int ContributionCount,
    DateTimeOffset? LastDonationDate,
    int EventsSupportedCount,
    string? PreferredPaymentMethod,
    bool IsAnonymous);

public record DonorDetailDto(
    string Name,
    string? Email,
    string? PhoneNumber,
    decimal TotalGiven,
    int ContributionCount,
    DateTimeOffset? LastDonationDate,
    IEnumerable<DonorContributionDto> RecentContributions,
    bool IsAnonymous);

public record DonorContributionDto(
    Guid Id,
    DateTimeOffset Date,
    decimal Amount,
    string Currency,
    string EventTitle,
    string Method,
    string Status);
