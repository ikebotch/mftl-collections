using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Events.Commands.AssignStaff;

public record AssignStaffToEventCommand(Guid EventId, IEnumerable<Guid> UserIds) : IRequest<bool>;

public class AssignStaffToEventCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<AssignStaffToEventCommand, bool>
{
    public async Task<bool> Handle(AssignStaffToEventCommand request, CancellationToken cancellationToken)
    {
        var eventData = await dbContext.Events.FindAsync(new object[] { request.EventId }, cancellationToken);
        if (eventData == null) return false;

        foreach (var userId in request.UserIds)
        {
            var user = await dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (user == null) continue;

            var exists = await dbContext.UserScopeAssignments.AnyAsync(a => 
                a.UserId == userId && 
                a.ScopeType == ScopeType.Event && 
                a.TargetId == request.EventId && 
                a.Role == "Collector", cancellationToken);

            if (!exists)
            {
                dbContext.UserScopeAssignments.Add(new UserScopeAssignment
                {
                    UserId = userId,
                    ScopeType = ScopeType.Event,
                    TargetId = request.EventId,
                    Role = "Collector",
                    BranchId = eventData.BranchId,
                    TenantId = eventData.TenantId
                });

                // Raise Domain Event
                user.AddDomainEvent(new Domain.Events.CollectorAssignedEvent(
                    userId,
                    eventData.TenantId,
                    eventData.BranchId,
                    request.EventId,
                    eventData.Title,
                    user.Name,
                    user.Email));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
