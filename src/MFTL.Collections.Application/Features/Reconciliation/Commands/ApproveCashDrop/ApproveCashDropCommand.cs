using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Events;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Reconciliation.Commands.ApproveCashDrop;

[HasPermission("reconciliation.approve")]
public record ApproveCashDropCommand(Guid CashDropId) : IRequest<bool>;

public class ApproveCashDropCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<ApproveCashDropCommand, bool>
{
    public async Task<bool> Handle(ApproveCashDropCommand request, CancellationToken cancellationToken)
    {
        var cashDrop = await dbContext.CashDrops
            .Include(c => c.Collector)
            .FirstOrDefaultAsync(c => c.Id == request.CashDropId, cancellationToken);
            
        if (cashDrop == null) return false;

        var currentUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Auth0Id == currentUserService.UserId, cancellationToken);
            
        if (currentUser == null) throw new UnauthorizedAccessException("Current user not found.");

        cashDrop.Status = CashDropStatus.Approved;
        cashDrop.ApprovedByUserId = currentUser.Id;
        cashDrop.ApprovedByUser = currentUser;
        cashDrop.ApprovedAt = DateTimeOffset.UtcNow;

        // Raise Domain Event
        cashDrop.AddDomainEvent(new CashDropApprovedEvent(
            cashDrop.Id,
            cashDrop.TenantId,
            cashDrop.BranchId,
            cashDrop.CollectorId,
            cashDrop.Amount,
            cashDrop.Currency,
            cashDrop.Collector.Name));

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
