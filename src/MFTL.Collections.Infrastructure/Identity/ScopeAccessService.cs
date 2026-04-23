using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Infrastructure.Identity;

public sealed class ScopeAccessService(CollectionsDbContext dbContext, ICurrentUserService currentUserService) : IScopeAccessService
{
    public async Task<bool> HasAccessToTenantAsync(Guid tenantId)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        // Platform admins see everything. 
        // Tenant admins see their own tenant.
        return await dbContext.UserScopeAssignments
            .AnyAsync(s => s.User.Auth0Id == userId && 
                          (s.ScopeType == ScopeType.Platform || 
                          (s.ScopeType == ScopeType.Tenant && s.TargetId == tenantId)));
    }

    public async Task<bool> HasAccessToEventAsync(Guid eventId)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        // Get the event to check its tenant
        var evt = await dbContext.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        if (evt == null) return false;

        return await dbContext.UserScopeAssignments
            .AnyAsync(s => s.User.Auth0Id == userId && 
                          (s.ScopeType == ScopeType.Platform || 
                          (s.ScopeType == ScopeType.Tenant && s.TargetId == evt.TenantId) ||
                          (s.ScopeType == ScopeType.Event && s.TargetId == eventId)));
    }

    public async Task<bool> HasAccessToRecipientFundAsync(Guid fundId)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        var fund = await dbContext.RecipientFunds
            .Include(f => f.Event)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fundId);
        if (fund == null) return false;

        return await dbContext.UserScopeAssignments
            .AnyAsync(s => s.User.Auth0Id == userId && 
                          (s.ScopeType == ScopeType.Platform || 
                          (s.ScopeType == ScopeType.Tenant && s.TargetId == fund.Event.TenantId) ||
                          (s.ScopeType == ScopeType.Event && s.TargetId == fund.EventId) ||
                          (s.ScopeType == ScopeType.RecipientFund && s.TargetId == fundId)));
    }

    public async Task<IEnumerable<Guid>> GetAccessibleEventIdsAsync(Guid tenantId)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return Enumerable.Empty<Guid>();

        var scopes = await dbContext.UserScopeAssignments
            .Where(s => s.User.Auth0Id == userId)
            .ToListAsync();

        // Platform or Tenant admin gets all events in the tenant
        if (scopes.Any(s => s.ScopeType == ScopeType.Platform || (s.ScopeType == ScopeType.Tenant && s.TargetId == tenantId)))
        {
            return await dbContext.Events.Where(e => e.TenantId == tenantId).Select(e => e.Id).ToListAsync();
        }

        // Otherwise only specifically assigned events
        return scopes.Where(s => s.ScopeType == ScopeType.Event && s.TargetId.HasValue)
                     .Select(s => s.TargetId!.Value)
                     .ToList();
    }
}
