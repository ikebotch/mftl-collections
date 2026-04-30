using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Collectors.Queries.GetPinStatus;

public record GetPinStatusQuery() : IRequest<CollectorPinStatusResponse>;

public class GetPinStatusQueryHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext) : IRequestHandler<GetPinStatusQuery, CollectorPinStatusResponse>
{
    public async Task<CollectorPinStatusResponse> Handle(GetPinStatusQuery request, CancellationToken cancellationToken)
    {
        var auth0Id = currentUserService.UserId;
        if (string.IsNullOrEmpty(auth0Id)) throw new UnauthorizedAccessException();

        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");

        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, cancellationToken);

        if (user == null) throw new KeyNotFoundException("User not found.");

        var hasCollectorRole = user.ScopeAssignments.Any(a => 
            a.Role == "Collector" && 
            (a.ScopeType == Domain.Entities.ScopeType.Tenant && a.TargetId == tenantId ||
             a.ScopeType == Domain.Entities.ScopeType.Branch && dbContext.Branches.Any(b => b.Id == a.TargetId && b.TenantId == tenantId) ||
             a.ScopeType == Domain.Entities.ScopeType.Event && dbContext.Events.Any(e => e.Id == a.TargetId && e.TenantId == tenantId) ||
             a.ScopeType == Domain.Entities.ScopeType.RecipientFund && dbContext.RecipientFunds.Any(f => f.Id == a.TargetId && f.TenantId == tenantId) ||
             a.ScopeType == Domain.Entities.ScopeType.Platform));

        if (!hasCollectorRole)
        {
            throw new UnauthorizedAccessException("User does not have collector access in this tenant.");
        }

        var hasPin = await dbContext.CollectorPins
            .AnyAsync(p => p.UserId == user.Id && p.TenantId == tenantId, cancellationToken);

        return new CollectorPinStatusResponse(hasPin);
    }
}
