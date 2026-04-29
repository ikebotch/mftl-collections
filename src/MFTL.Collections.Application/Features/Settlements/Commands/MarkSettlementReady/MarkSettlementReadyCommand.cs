using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Events;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Settlements.Commands.MarkSettlementReady;

[HasPermission("settlements.manage")]
public record MarkSettlementReadyCommand(Guid SettlementId) : IRequest<bool>;

public class MarkSettlementReadyCommandHandler(
    IApplicationDbContext dbContext) : IRequestHandler<MarkSettlementReadyCommand, bool>
{
    public async Task<bool> Handle(MarkSettlementReadyCommand request, CancellationToken cancellationToken)
    {
        var settlement = await dbContext.Settlements
            .Include(s => s.Collector)
            .FirstOrDefaultAsync(s => s.Id == request.SettlementId, cancellationToken);
            
        if (settlement == null) return false;

        // Raise Domain Event
        settlement.AddDomainEvent(new SettlementReadyEvent(
            settlement.Id,
            settlement.TenantId,
            settlement.BranchId,
            settlement.CollectorId,
            settlement.Collector.Name,
            settlement.Amount,
            settlement.Currency));

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
