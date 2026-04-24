using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Collectors.Commands.CreateCollector;

public record CreateCollectorCommand(
    string Name,
    string Email,
    string? PhoneNumber,
    IEnumerable<Guid>? AssignedEventIds,
    IEnumerable<Guid>? AssignedFundIds) : IRequest<CollectorDto>;

public class CreateCollectorCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<CreateCollectorCommand, CollectorDto>
{
    public async Task<CollectorDto> Handle(CreateCollectorCommand request, CancellationToken cancellationToken)
    {
        // Pragmatic implementation: find or create user, then assign roles
        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user == null)
        {
            user = new User
            {
                Name = request.Name,
                Email = request.Email,
                Auth0Id = $"manual|{Guid.NewGuid():N}", // Placeholder for manual invite
                IsActive = true
            };
            dbContext.Users.Add(user);
        }

        // Add scope assignments
        if (request.AssignedEventIds != null)
        {
            foreach (var eventId in request.AssignedEventIds)
            {
                if (!user.ScopeAssignments.Any(s => s.ScopeType == ScopeType.Event && s.TargetId == eventId))
                {
                    user.ScopeAssignments.Add(new UserScopeAssignment
                    {
                        ScopeType = ScopeType.Event,
                        TargetId = eventId,
                        Role = "Collector"
                    });
                }
            }
        }

        if (request.AssignedFundIds != null)
        {
            foreach (var fundId in request.AssignedFundIds)
            {
                if (!user.ScopeAssignments.Any(s => s.ScopeType == ScopeType.RecipientFund && s.TargetId == fundId))
                {
                    user.ScopeAssignments.Add(new UserScopeAssignment
                    {
                        ScopeType = ScopeType.RecipientFund,
                        TargetId = fundId,
                        Role = "Collector"
                    });
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CollectorDto(
            Id: user.Id,
            Name: user.Name,
            Email: user.Email,
            PhoneNumber: request.PhoneNumber,
            Status: "Active",
            AssignedEventCount: request.AssignedEventIds?.Count() ?? 0,
            AssignedFundCount: request.AssignedFundIds?.Count() ?? 0,
            TotalCollectedToday: 0,
            TotalCollectedMonth: 0,
            LastActiveAt: null
        );
    }
}
