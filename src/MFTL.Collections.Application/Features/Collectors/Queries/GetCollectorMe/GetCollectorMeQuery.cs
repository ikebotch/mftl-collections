using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorMe;

public sealed record CollectorMeDto(
    Guid Id,
    string Name,
    string Email,
    string Status,
    int AssignedEventCount,
    int AssignedFundCount,
    decimal TotalCollectedToday,
    int ReceiptsIssuedToday,
    DateTimeOffset? LastActiveAt,
    bool HasAssignments,
    string? BlockedReason,
    string? PhoneNumber = null,
    IEnumerable<Guid>? EventIds = null,
    IEnumerable<Guid>? FundIds = null);

public record GetCollectorMeQuery(string? ExplicitUserId = null) : IRequest<CollectorMeDto>;

public class GetCollectorMeQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService) : IRequestHandler<GetCollectorMeQuery, CollectorMeDto>
{
    public async Task<CollectorMeDto> Handle(GetCollectorMeQuery request, CancellationToken cancellationToken)
    {
        var auth0Id = request.ExplicitUserId ?? currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(auth0Id))
        {
            throw new UnauthorizedAccessException("Collector authentication is required.");
        }

        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, cancellationToken);

        if (user == null && !string.IsNullOrWhiteSpace(request.ExplicitUserId))
        {
            user = await dbContext.Users
                .Include(u => u.ScopeAssignments)
                .Where(u => u.IsActive && u.ScopeAssignments.Any(a => a.Role == "Collector"))
                .OrderBy(u => u.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (user == null)
        {
            throw new KeyNotFoundException("Collector profile not found.");
        }

        var assignments = user.ScopeAssignments.Where(a => a.Role == "Collector").ToList();
        var eventCount = assignments.Count(a => a.ScopeType == ScopeType.Event);
        var fundCount = assignments.Count(a => a.ScopeType == ScopeType.RecipientFund);
        var hasAssignments = eventCount > 0 || fundCount > 0;
        var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);

        var receiptsToday = await dbContext.Receipts
            .Where(r => r.RecordedByUserId == user.Id && r.IssuedAt >= today)
            .Include(r => r.Contribution)
            .ToListAsync(cancellationToken);

        var lastActive = await dbContext.Receipts
            .Where(r => r.RecordedByUserId == user.Id)
            .OrderByDescending(r => r.IssuedAt)
            .Select(r => (DateTimeOffset?)r.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new CollectorMeDto(
            user.Id,
            user.Name,
            user.Email,
            user.IsActive ? "Active" : "Inactive",
            eventCount,
            fundCount,
            receiptsToday.Sum(r => r.Contribution?.Amount ?? 0),
            receiptsToday.Count,
            lastActive,
            hasAssignments,
            user.IsActive
                ? (hasAssignments ? null : "No active campaign or fund assignments established for this collector.")
                : "Collector is inactive.",
            user.PhoneNumber,
            assignments.Where(a => a.ScopeType == ScopeType.Event).Select(a => a.TargetId ?? Guid.Empty),
            assignments.Where(a => a.ScopeType == ScopeType.RecipientFund).Select(a => a.TargetId ?? Guid.Empty));
    }
}
