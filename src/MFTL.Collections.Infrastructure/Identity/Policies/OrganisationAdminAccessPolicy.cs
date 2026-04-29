using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Infrastructure.Identity.Policies;

public class OrganisationAdminAccessPolicy(AccessContext context) : AccessPolicyBase(context)
{
    public override bool CanManageUsers(string scope) => true;

    public override IQueryable<Tenant> FilterTenants(IQueryable<Tenant> query) => 
        query.Where(t => Context.TenantIds.Contains(t.Id));

    public override IQueryable<Branch> FilterBranches(IQueryable<Branch> query) => 
        query.Where(b => Context.TenantIds.Contains(b.TenantId));

    public override IQueryable<Event> FilterEvents(IQueryable<Event> query) => 
        query.Where(e => Context.TenantIds.Contains(e.TenantId) || !e.IsPrivate);

    public override IQueryable<RecipientFund> FilterFunds(IQueryable<RecipientFund> query) => 
        query.Where(f => Context.TenantIds.Contains(f.TenantId));

    public override IQueryable<Contribution> FilterCollections(IQueryable<Contribution> query) => 
        query.Where(c => Context.TenantIds.Contains(c.TenantId));

    public override IQueryable<User> FilterUsers(IQueryable<User> query) => 
        query.Where(u => u.ScopeAssignments.Any(s => 
            Context.TenantIds.Contains(s.TargetId ?? Guid.Empty) ||
            Context.BranchIds.Contains(s.TargetId ?? Guid.Empty) ||
            Context.EventIds.Contains(s.TargetId ?? Guid.Empty) ||
            Context.FundIds.Contains(s.TargetId ?? Guid.Empty)));
}
