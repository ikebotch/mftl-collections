using MFTL.Collections.Contracts.Common;
namespace MFTL.Collections.Contracts.Responses;


public record RecipientDashboardDto(
    Guid RecipientFundId,
    string Name,
    decimal TargetAmount,
    IEnumerable<CurrencyTotalDto> Totals,
    decimal ProgressPercentage,
    int ContributionCount,
    IEnumerable<RecentContributionDto> RecentContributions);

public record AdminDashboardDto(
    int TotalEvents,
    int TotalContributions,
    IEnumerable<CurrencyTotalDto> Totals,
    int ActiveRecipientFunds,
    int TotalCollectors,
    int TotalDonors,
    int TotalReceipts,
    IEnumerable<RecentContributionDto> RecentContributions);

public record RecentContributionDto(
    string ContributorName,
    decimal Amount,
    string Currency,
    DateTimeOffset Date,
    string Status,
    string? EventTitle = null,
    string? PaymentMethod = null);

public record EventDashboardDto(
    Guid EventId,
    string Title,
    IEnumerable<CurrencyTotalDto> Totals,
    int ContributionCount,
    int DonorCount,
    IEnumerable<RecentContributionDto> RecentContributions);

public record SettlementDto(
    Guid Id,
    string CollectorName,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset Date,
    string? Note = null);
