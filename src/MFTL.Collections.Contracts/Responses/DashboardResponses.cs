namespace MFTL.Collections.Contracts.Responses;

public record RecipientDashboardDto(
    Guid RecipientFundId,
    string Name,
    decimal TargetAmount,
    decimal CollectedAmount,
    decimal ProgressPercentage,
    int ContributionCount,
    IEnumerable<RecentContributionDto> RecentContributions);

public record RecentContributionDto(
    string ContributorName,
    decimal Amount,
    DateTimeOffset Date,
    string Status);
