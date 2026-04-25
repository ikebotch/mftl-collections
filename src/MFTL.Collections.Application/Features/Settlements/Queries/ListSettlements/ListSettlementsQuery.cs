using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Settlements.Queries.ListSettlements;

public record ListSettlementsQuery() : IRequest<IEnumerable<SettlementDto>>;

public class ListSettlementsHandler(IApplicationDbContext dbContext) : IRequestHandler<ListSettlementsQuery, IEnumerable<SettlementDto>>
{
    public async Task<IEnumerable<SettlementDto>> Handle(ListSettlementsQuery request, CancellationToken cancellationToken)
    {
        var settlements = await dbContext.Settlements
            .Include(s => s.Collector)
            .OrderByDescending(s => s.SettlementDate)
            .ToListAsync(cancellationToken);

        if (!settlements.Any())
        {
            // Return mock data if empty for demo purposes
            return new List<SettlementDto>
            {
                new(Guid.NewGuid(), "Samuel Osei", 1250.00m, "GHS", "Awaiting Handover", DateTimeOffset.UtcNow.AddDays(-1)),
                new(Guid.NewGuid(), "Grace Mensah", 840.50m, "GHS", "In Review", DateTimeOffset.UtcNow.AddDays(-1)),
                new(Guid.NewGuid(), "Isaac Boateng", 3200.00m, "GHS", "Awaiting Handover", DateTimeOffset.UtcNow.AddDays(-2))
            };
        }

        return settlements.Select(s => new SettlementDto(
            s.Id,
            s.Collector.Name,
            s.Amount,
            s.Currency,
            s.Status,
            s.SettlementDate,
            s.Note
        ));
    }
}
