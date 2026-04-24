namespace MFTL.Collections.Contracts.Requests;

public record DonorDto(
    Guid Id,
    string Name,
    string Email,
    string? PhoneNumber,
    bool IsAnonymous,
    decimal TotalGiven,
    int ContributionCount,
    DateTimeOffset? LastDonationAt);
