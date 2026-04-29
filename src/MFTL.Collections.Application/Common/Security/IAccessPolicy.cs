using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Common.Security;

public interface IAccessPolicy
{
    // Scoped Access Checks
    bool CanAccessTenant(Guid tenantId);
    bool CanAccessBranch(Guid branchId);
    bool CanAccessEvent(Guid eventId);
    bool CanAccessFund(Guid fundId);
    
    // Action-Level Checks
    bool CanManageUsers(string scope);
    bool CanRecordCollection(Guid eventId, Guid fundId);
    bool CanViewPrivateEvent(Guid eventId);
    
    // Query Filtering (Returning Queryable filters)
    IQueryable<Tenant> FilterTenants(IQueryable<Tenant> query);
    IQueryable<Branch> FilterBranches(IQueryable<Branch> query);
    IQueryable<Event> FilterEvents(IQueryable<Event> query);
    IQueryable<RecipientFund> FilterFunds(IQueryable<RecipientFund> query);
    IQueryable<Contribution> FilterCollections(IQueryable<Contribution> query);
    IQueryable<User> FilterUsers(IQueryable<User> query);
}
