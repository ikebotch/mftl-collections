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

        return await dbContext.UserScopeAssignments
            .AnyAsync(s => s.User.Auth0Id == userId && 
                          (s.ScopeType == ScopeType.Platform || 
                          (s.ScopeType == ScopeType.Organisation && s.TargetId == tenantId)));
    }

    public async Task<bool> HasAccessToBranchAsync(Guid branchId)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        var branch = await dbContext.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId);
        if (branch == null) return false;

        return await dbContext.UserScopeAssignments
            .AnyAsync(s => s.User.Auth0Id == userId && 
                          (s.ScopeType == ScopeType.Platform || 
                          (s.ScopeType == ScopeType.Organisation && s.TargetId == branch.TenantId) ||
                          (s.ScopeType == ScopeType.Branch && s.TargetId == branchId)));
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
                           s.ScopeType == ScopeType.Organisation || 
                          (s.ScopeType == ScopeType.Branch && s.TargetId == @event.BranchId) ||
                          (s.ScopeType == ScopeType.Event && s.TargetId == eventId)));
    }

    public async Task<bool> HasAccessToRecipientFundAsync(Guid fundId)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        var fund = await dbContext.RecipientFunds.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fundId);
        if (fund == null) return false;

        return await dbContext.UserScopeAssignments
            .AnyAsync(s => s.User.Auth0Id == userId && 
                          (s.ScopeType == ScopeType.Platform || 
                           s.ScopeType == ScopeType.Organisation || 
                          (s.ScopeType == ScopeType.Branch && s.TargetId == fund.BranchId) ||
                          (s.ScopeType == ScopeType.Event && s.TargetId == fund.EventId) ||
                          (s.ScopeType == ScopeType.RecipientFund && s.TargetId == fundId)));
    }

    public async Task<IEnumerable<Guid>> GetAccessibleEventIdsAsync(Guid tenantId, Guid? branchId = null)
    {
        var userId = currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return Enumerable.Empty<Guid>();

        var scopes = await dbContext.UserScopeAssignments
            .Where(s => s.User.Auth0Id == userId)
            .ToListAsync();

        if (scopes.Any(s => s.ScopeType == ScopeType.Platform || (s.ScopeType == ScopeType.Organisation && s.TargetId == tenantId)))
        {
            var query = dbContext.Events.Where(e => e.TenantId == tenantId);
            if (branchId.HasValue) query = query.Where(e => e.BranchId == branchId.Value);
            return await query.Select(e => e.Id).ToListAsync();
        }

        var assignedBranchIds = scopes.Where(s => s.ScopeType == ScopeType.Branch).Select(s => s.TargetId).ToList();
        var assignedEventIds = scopes.Where(s => s.ScopeType == ScopeType.Event).Select(s => s.TargetId).ToList();

        var eventsQuery = dbContext.Events.Where(e => e.TenantId == tenantId);
        
        if (branchId.HasValue)
        {
            // If filtering by branch, user must have access to that branch or specific events in it
            if (!assignedBranchIds.Contains(branchId.Value))
            {
                eventsQuery = eventsQuery.Where(e => assignedEventIds.Contains(e.Id) && e.BranchId == branchId.Value);
            }
            else
            {
                eventsQuery = eventsQuery.Where(e => e.BranchId == branchId.Value);
            }
        }
        else
        {
            eventsQuery = eventsQuery.Where(e => assignedBranchIds.Contains(e.BranchId) || assignedEventIds.Contains(e.Id));
        }

        return await eventsQuery.Select(e => e.Id).ToListAsync();
    }
}
