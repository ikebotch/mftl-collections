using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Events;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Reconciliation.Commands.CloseEod;

[HasPermission("reconciliation.close")]
public record CloseEodCommand(Guid BranchId, string Title) : IRequest<Guid>;

public class CloseEodCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<CloseEodCommand, Guid>
{
    public async Task<Guid> Handle(CloseEodCommand request, CancellationToken cancellationToken)
    {
        var branch = await dbContext.Branches
            .FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken);
            
        if (branch == null) throw new KeyNotFoundException("Branch not found.");

        var currentUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Auth0Id == currentUserService.UserId, cancellationToken);
            
        if (currentUser == null) throw new UnauthorizedAccessException("Current user not found in database.");

        // Calculate total amount from completed contributions for the day at this branch
        var today = DateTimeOffset.UtcNow.Date;
        var totalAmount = await dbContext.Contributions
            .Where(c => c.BranchId == request.BranchId && c.Status == Domain.Enums.ContributionStatus.Completed && c.CreatedAt >= today)
            .SumAsync(c => c.Amount, cancellationToken);

        var report = new EodReport
        {
            Id = Guid.NewGuid(),
            BranchId = request.BranchId,
            TenantId = branch.TenantId,
            Title = request.Title,
            TotalAmount = totalAmount,
            Currency = "GHS",
            ClosedAt = DateTimeOffset.UtcNow,
            ClosedByUserId = currentUser.Id,
            ClosedByUser = currentUser
        };

        dbContext.EodReports.Add(report);

        // Raise Domain Event
        report.AddDomainEvent(new EodClosedEvent(
            request.BranchId,
            branch.TenantId,
            branch.Name,
            totalAmount,
            "GHS"));

        await dbContext.SaveChangesAsync(cancellationToken);

        return report.Id;
    }
}
