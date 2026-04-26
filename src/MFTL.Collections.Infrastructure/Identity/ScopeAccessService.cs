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

        // Check platform admin or direct tenant assignment
        return await dbContext.UserScopeAssignments
            .AnyAsync(s => s.User.Auth0Id == userId && 
                          (s.ScopeType == ScopeType.Platform || 
                          (s.ScopeType == ScopeType.Tenant && s.TargetId == tenantId)));
    }

    public async Task<bool> HasAccessToEventAsync(Guid eventId)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        var @event = await dbContext.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        if (@event == null) return false;

        return await dbContext.UserScopeAssignments
            .AnyAsync(s => s.User.Auth0Id == userId && 
                          (s.ScopeType == ScopeType.Platform || 
                           s.ScopeType == ScopeType.Tenant || 
                          (s.ScopeType == ScopeType.Branch && s.TargetId == @event.BranchId) ||
                          (s.ScopeType == ScopeType.Event && s.TargetId == eventId)));
    }

    public async Task<bool> HasAccessToRecipientFundAsync(Guid fundId)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        var fund = await dbContext.RecipientFunds.AsNoTracking().Include(f => f.Event).FirstOrDefaultAsync(f => f.Id == fundId);
        if (fund == null) return false;

        return await dbContext.UserScopeAssignments
            .AnyAsync(s => s.User.Auth0Id == userId && 
                          (s.ScopeType == ScopeType.Platform || 
                           s.ScopeType == ScopeType.Tenant || 
                          (s.ScopeType == ScopeType.Branch && s.TargetId == fund.Event.BranchId) ||
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

        if (scopes.Any(s => s.ScopeType == ScopeType.Platform || (s.ScopeType == ScopeType.Tenant && s.TargetId == tenantId)))
        {
            return await dbContext.Events.Where(e => e.TenantId == tenantId).Select(e => e.Id).ToListAsync();
        }

        var branchIds = scopes.Where(s => s.ScopeType == ScopeType.Branch && s.TargetId.HasValue).Select(s => s.TargetId!.Value).ToList();
        var directEventIds = scopes.Where(s => s.ScopeType == ScopeType.Event && s.TargetId.HasValue).Select(s => s.TargetId!.Value).ToList();

        return await dbContext.Events
            .Where(e => e.TenantId == tenantId && (branchIds.Contains(e.BranchId) || directEventIds.Contains(e.Id)))
            .Select(e => e.Id)
            .ToListAsync();
    }
}
