using MFTL.Collections.Contracts.Common;

namespace MFTL.Collections.Contracts.Requests;

public record CreateRecipientFundRequest(
    Guid EventId,
    string Name,
    string Description,
    decimal TargetAmount);

public record RecipientFundDto(
    Guid Id,
    Guid EventId,
    string Name,
    string Description,
    decimal TargetAmount,
    bool IsActive,
    IEnumerable<CurrencyTotalDto> Totals);
