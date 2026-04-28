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
    IEnumerable<Guid>? FundIds = null,
    bool HasPin = false);

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

        if (user == null)
        {
            throw new KeyNotFoundException("Collector profile not found.");
        }

        var assignments = user.ScopeAssignments.Where(a => a.Role == "Collector").ToList();
        var orgIds = assignments
            .Where(a => a.ScopeType == ScopeType.Organisation && a.TargetId.HasValue)
            .Select(a => a.TargetId!.Value)
            .ToList();

        var branchIds = assignments
            .Where(a => a.ScopeType == ScopeType.Branch && a.TargetId.HasValue)
            .Select(a => a.TargetId!.Value)
            .ToList();

        var directEventIds = assignments
            .Where(a => a.ScopeType == ScopeType.Event && a.TargetId.HasValue)
            .Select(a => a.TargetId!.Value)
            .ToList();

        var directFundIds = assignments
            .Where(a => a.ScopeType == ScopeType.RecipientFund && a.TargetId.HasValue)
            .Select(a => a.TargetId!.Value)
            .ToList();

        var funds = await dbContext.RecipientFunds
            .Include(f => f.Event)
            .Where(fund => 
                directFundIds.Contains(fund.Id) || 
                directEventIds.Contains(fund.EventId) ||
                orgIds.Contains(fund.Event.TenantId) ||
                (fund.Event.BranchId.HasValue && branchIds.Contains(fund.Event.BranchId.Value)))
            .ToListAsync(cancellationToken);

        var fundCount = funds.Count;
        var eventCount = funds.Select(f => f.EventId).Concat(directEventIds).Distinct().Count();
        
        var hasAssignments = eventCount > 0 || fundCount > 0 || orgIds.Count > 0 || branchIds.Count > 0;
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
            directEventIds,
            directFundIds,
            !string.IsNullOrEmpty(user.Pin));
    }
}
