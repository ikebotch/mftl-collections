using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Infrastructure.Identity.Policies;

public class PlatformAdminAccessPolicy(AccessContext context) : AccessPolicyBase(context)
{
    public override bool CanManageUsers(string scope) => true;

    public override IQueryable<Tenant> FilterTenants(IQueryable<Tenant> query) => query;
    public override IQueryable<Branch> FilterBranches(IQueryable<Branch> query) => query;
    public override IQueryable<Event> FilterEvents(IQueryable<Event> query) => query;
    public override IQueryable<RecipientFund> FilterFunds(IQueryable<RecipientFund> query) => query;
    public override IQueryable<Contribution> FilterCollections(IQueryable<Contribution> query) => query;
    public override IQueryable<User> FilterUsers(IQueryable<User> query) => query;
}
