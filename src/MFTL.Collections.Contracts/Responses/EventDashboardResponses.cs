namespace MFTL.Collections.Contracts.Responses;

public record EventDashboardDto(
    Guid EventId,
    string Title,
    decimal TotalTarget,
    decimal TotalCollected,
    int TotalContributions,
    IEnumerable<FundSummaryDto> Funds);

public record FundSummaryDto(Guid Id, string Name, decimal Collected, decimal Target);
