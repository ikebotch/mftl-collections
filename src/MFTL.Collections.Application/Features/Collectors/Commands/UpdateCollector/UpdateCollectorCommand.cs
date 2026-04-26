using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Collectors.Commands.UpdateCollector;

public record UpdateCollectorCommand(
    Guid Id,
    string Name,
    string Email,
    string? Phone = null,
    string? Status = null,
    IEnumerable<Guid>? EventIds = null,
    IEnumerable<Guid>? FundIds = null) : IRequest<bool>;

public class UpdateCollectorCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<UpdateCollectorCommand, bool>
{
    public async Task<bool> Handle(UpdateCollectorCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user == null) return false;

        user.Name = request.Name;
        user.Email = request.Email;
        user.IsActive = request.Status?.ToLower() != "inactive";

        // Update Event Assignments
        if (request.EventIds != null)
        {
            // Remove existing event assignments
            var existingEventAssignments = user.ScopeAssignments
                .Where(a => a.ScopeType == ScopeType.Event && a.Role == "Collector")
                .ToList();

            foreach (var assignment in existingEventAssignments)
            {
                user.ScopeAssignments.Remove(assignment);
            }

            // Add new assignments
            foreach (var eventId in request.EventIds)
            {
                user.ScopeAssignments.Add(new UserScopeAssignment
                {
                    UserId = user.Id,
                    ScopeType = ScopeType.Event,
                    TargetId = eventId,
                    Role = "Collector"
                });
            }
        }

        // Update Fund Assignments
        if (request.FundIds != null)
        {
            var existingFundAssignments = user.ScopeAssignments
                .Where(a => a.ScopeType == ScopeType.RecipientFund && a.Role == "Collector")
                .ToList();

            foreach (var assignment in existingFundAssignments)
            {
                user.ScopeAssignments.Remove(assignment);
            }

            foreach (var fundId in request.FundIds)
            {
                user.ScopeAssignments.Add(new UserScopeAssignment
                {
                    UserId = user.Id,
                    ScopeType = ScopeType.RecipientFund,
                    TargetId = fundId,
                    Role = "Collector"
                });
            }
        }
        
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
