using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Events;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Reconciliation.Commands.SubmitCashDrop;

[HasPermission("self.record_drop")]
public record SubmitCashDropCommand(decimal Amount, string? Note) : IRequest<Guid>;

public class SubmitCashDropCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<SubmitCashDropCommand, Guid>
{
    public async Task<Guid> Handle(SubmitCashDropCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Auth0Id == currentUserService.UserId, cancellationToken);
            
        if (user == null) throw new UnauthorizedAccessException("User not found.");

        // We need branchId and tenantId from scope
        var scope = await dbContext.UserScopeAssignments
            .Where(a => a.UserId == user.Id && a.BranchId != null)
            .Select(a => new { a.BranchId, a.TenantId })
            .FirstOrDefaultAsync(cancellationToken);

        if (scope == null) throw new InvalidOperationException("User is not assigned to any branch.");

        var cashDrop = new CashDrop
        {
            Id = Guid.NewGuid(),
            CollectorId = user.Id,
            Collector = user,
            Amount = request.Amount,
            Currency = "GHS",
            Status = CashDropStatus.Submitted,
            Note = request.Note,
            BranchId = scope.BranchId.Value,
            TenantId = scope.TenantId.Value
        };

        dbContext.CashDrops.Add(cashDrop);

        // Raise Domain Event
        cashDrop.AddDomainEvent(new CashDropSubmittedEvent(
            cashDrop.Id,
            cashDrop.TenantId,
            cashDrop.BranchId,
            user.Id,
            request.Amount,
            "GHS",
            user.Name));

        await dbContext.SaveChangesAsync(cancellationToken);

        return cashDrop.Id;
    }
}
