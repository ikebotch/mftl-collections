using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Branch> Branches { get; }
    DbSet<Event> Events { get; }
    DbSet<RecipientFund> RecipientFunds { get; }
    DbSet<Contributor> Contributors { get; }
    DbSet<Contribution> Contributions { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Receipt> Receipts { get; }
    DbSet<User> Users { get; }
    DbSet<UserScopeAssignment> UserScopeAssignments { get; }
    DbSet<Settlement> Settlements { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SmsTemplate> SmsTemplates { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
