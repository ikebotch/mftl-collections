using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Event> Events { get; }
    DbSet<RecipientFund> RecipientFunds { get; }
    DbSet<Contributor> Contributors { get; }
    DbSet<Contribution> Contributions { get; }
    DbSet<Payment> Payments { get; }
    DbSet<User> Users { get; }
    DbSet<UserScopeAssignment> UserScopeAssignments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
