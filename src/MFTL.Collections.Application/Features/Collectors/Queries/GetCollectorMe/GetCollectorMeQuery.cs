using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorMe;

public record GetCollectorMeQuery() : IRequest<CollectorMeDto>;

public class GetCollectorMeQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<GetCollectorMeQuery, CollectorMeDto>
{
    public async Task<CollectorMeDto> Handle(GetCollectorMeQuery request, CancellationToken cancellationToken)
    {
        var auth0Id = currentUserService.UserId;
        if (string.IsNullOrEmpty(auth0Id))
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, cancellationToken);

        if (user == null)
        {
            throw new KeyNotFoundException("Collector profile not found.");
        }

        var assignments = user.ScopeAssignments.Where(a => a.Role == "Collector").ToList();
        var eventCount = assignments.Count(a => a.ScopeType == ScopeType.Event);
        var fundCount = assignments.Count(a => a.ScopeType == ScopeType.RecipientFund);

        var today = DateTimeOffset.UtcNow.Date;
        var receiptsToday = await dbContext.Receipts
            .Where(r => r.RecordedByUserId == user.Id && r.IssuedAt >= today)
            .Include(r => r.Contribution)
            .ToListAsync(cancellationToken);

        var totalToday = receiptsToday.Sum(r => r.Contribution.Amount);
        var countToday = receiptsToday.Count;

        var lastActive = await dbContext.Receipts
            .Where(r => r.RecordedByUserId == user.Id)
            .OrderByDescending(r => r.IssuedAt)
            .Select(r => (DateTimeOffset?)r.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new CollectorMeDto(
            Id: user.Id,
            Name: user.Name,
            Email: user.Email,
            Status: user.IsActive ? "Active" : "Inactive",
            AssignedEventCount: eventCount,
            AssignedFundCount: fundCount,
            TotalCollectedToday: totalToday,
            ReceiptsIssuedToday: countToday,
            LastActiveAt: lastActive
        );
    }
}
