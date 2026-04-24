namespace MFTL.Collections.Contracts.Responses;

public record RecipientDashboardDto(
    Guid RecipientFundId,
    string Name,
    decimal TargetAmount,
    decimal CollectedAmount,
    decimal ProgressPercentage,
    int ContributionCount,
    IEnumerable<RecentContributionDto> RecentContributions);

public record AdminDashboardDto(
    int TotalEvents,
    int TotalContributions,
    decimal TotalCollected,
    int ActiveRecipientFunds,
    int TotalCollectors,
    int TotalDonors,
    int TotalReceipts,
    IEnumerable<RecentContributionDto> RecentContributions);

public record RecentContributionDto(
    string ContributorName,
    decimal Amount,
    DateTimeOffset Date,
    string Status,
    string? EventTitle = null,
    string? PaymentMethod = null);
