using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorAssignments;

public record GetCollectorAssignmentsQuery() : IRequest<IEnumerable<CollectorAssignmentDto>>;

public class GetCollectorAssignmentsQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<GetCollectorAssignmentsQuery, IEnumerable<CollectorAssignmentDto>>
{
    public async Task<IEnumerable<CollectorAssignmentDto>> Handle(GetCollectorAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var auth0Id = currentUserService.UserId;
        if (string.IsNullOrEmpty(auth0Id))
        {
            return Enumerable.Empty<CollectorAssignmentDto>();
        }

        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, cancellationToken);

        if (user == null)
        {
            return Enumerable.Empty<CollectorAssignmentDto>();
        }

        var assignedEventIds = user.ScopeAssignments
            .Where(a => a.ScopeType == ScopeType.Event && a.Role == "Collector" && a.TargetId.HasValue)
            .Select(a => a.TargetId!.Value)
            .ToList();

        if (!assignedEventIds.Any())
        {
            return Enumerable.Empty<CollectorAssignmentDto>();
        }

        var events = await dbContext.Events
            .Where(e => assignedEventIds.Contains(e.Id))
            .Include(e => e.RecipientFunds)
            .ToListAsync(cancellationToken);

        return events.Select(e => new CollectorAssignmentDto(
            Id: e.Id,
            Title: e.Title,
            Location: "Main Site", // Placeholder or from metadata
            Date: e.CreatedAt.ToString("MMM dd, yyyy"),
            FundCount: e.RecipientFunds.Count
        ));
    }
}
