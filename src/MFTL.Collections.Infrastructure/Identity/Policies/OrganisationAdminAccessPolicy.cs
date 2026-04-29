using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Infrastructure.Identity.Policies;

public class OrganisationAdminAccessPolicy(AccessContext context) : AccessPolicyBase(context)
{
    public override bool CanManageUsers(string scope) => true;

    public override IQueryable<Tenant> FilterTenants(IQueryable<Tenant> query) => 
        query.Where(t => Context.TenantIds.Contains(t.Id));

    public override IQueryable<Branch> FilterBranches(IQueryable<Branch> query) => 
        query.Where(b => Context.TenantIds.Contains(b.TenantId) || Context.BranchIds.Contains(b.Id));

    public override IQueryable<Event> FilterEvents(IQueryable<Event> query) => 
        query.Where(e => Context.TenantIds.Contains(e.TenantId) || Context.EventIds.Contains(e.Id) || !e.IsPrivate);

    public override IQueryable<RecipientFund> FilterFunds(IQueryable<RecipientFund> query) => 
        query.Where(f => Context.TenantIds.Contains(f.TenantId) || Context.FundIds.Contains(f.Id) || Context.BranchIds.Contains(f.BranchId));

    public override IQueryable<Contribution> FilterCollections(IQueryable<Contribution> query) => 
        query.Where(c => Context.TenantIds.Contains(c.TenantId) || Context.BranchIds.Contains(c.BranchId) || (c.Receipt != null && c.Receipt.RecordedByUserId == Context.UserId));

    public override IQueryable<User> FilterUsers(IQueryable<User> query) => 
        query.Where(u => u.ScopeAssignments.Any(s => 
            Context.TenantIds.Contains(s.TargetId ?? Guid.Empty) ||
            Context.BranchIds.Contains(s.TargetId ?? Guid.Empty) ||
            Context.EventIds.Contains(s.TargetId ?? Guid.Empty) ||
            Context.FundIds.Contains(s.TargetId ?? Guid.Empty)));
}
