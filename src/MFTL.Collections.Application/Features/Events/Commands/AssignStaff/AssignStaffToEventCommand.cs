using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Events.Commands.AssignStaff;

public record AssignStaffToEventCommand(Guid EventId, IEnumerable<Guid> UserIds) : IRequest<bool>;

public class AssignStaffToEventCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<AssignStaffToEventCommand, bool>
{
    public async Task<bool> Handle(AssignStaffToEventCommand request, CancellationToken cancellationToken)
    {
        var @event = await dbContext.Events.AnyAsync(e => e.Id == request.EventId, cancellationToken);
        if (!@event) return false;

        foreach (var userId in request.UserIds)
        {
            var exists = await dbContext.UserScopeAssignments.AnyAsync(a => 
                a.UserId == userId && 
                a.ScopeType == ScopeType.Event && 
                a.TargetId == request.EventId && 
                a.Role == AppRoles.Collector, cancellationToken);

            if (!exists)
            {
                dbContext.UserScopeAssignments.Add(new UserScopeAssignment
                {
                    UserId = userId,
                    ScopeType = ScopeType.Event,
                    TargetId = request.EventId,
                    Role = AppRoles.Collector
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
