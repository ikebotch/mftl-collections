using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Infrastructure.Identity.Policies;

public abstract class AccessPolicyBase(AccessContext context) : IAccessPolicy
{
    protected readonly AccessContext Context = context;

    public virtual bool CanAccessTenant(Guid tenantId) => Context.TenantIds.Contains(tenantId) || Context.IsPlatformAdmin;
    
    public virtual bool CanAccessBranch(Guid branchId) => Context.BranchIds.Contains(branchId) || Context.IsPlatformAdmin;

    public virtual bool CanAccessEvent(Guid eventId) => Context.EventIds.Contains(eventId) || Context.IsPlatformAdmin;

    public virtual bool CanAccessFund(Guid fundId) => Context.FundIds.Contains(fundId) || Context.IsPlatformAdmin;

    public virtual bool CanManageUsers(string scope) => false;

    public virtual bool CanRecordCollection(Guid eventId, Guid fundId) => CanAccessEvent(eventId) && CanAccessFund(fundId);

    public virtual bool CanViewPrivateEvent(Guid eventId) => CanAccessEvent(eventId);

    public abstract IQueryable<Tenant> FilterTenants(IQueryable<Tenant> query);
    public abstract IQueryable<Branch> FilterBranches(IQueryable<Branch> query);
    public abstract IQueryable<Event> FilterEvents(IQueryable<Event> query);
    public abstract IQueryable<RecipientFund> FilterFunds(IQueryable<RecipientFund> query);
    public abstract IQueryable<Contribution> FilterCollections(IQueryable<Contribution> query);
    public abstract IQueryable<User> FilterUsers(IQueryable<User> query);
}
