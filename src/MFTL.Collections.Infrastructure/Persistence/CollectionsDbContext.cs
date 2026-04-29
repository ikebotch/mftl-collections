using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Common;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Application.Common.Interfaces;
using System.Linq.Expressions;
using MFTL.Collections.Infrastructure.Tenancy;

namespace MFTL.Collections.Infrastructure.Persistence;

public sealed class CollectionsDbContext(
    DbContextOptions<CollectionsDbContext> options, 
    ITenantContext tenantContext,
    IBranchContext branchContext) : DbContext(options), IApplicationDbContext
{
    private readonly ITenantContext _tenantContext = tenantContext;
    private readonly IBranchContext _branchContext = branchContext;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<RecipientFund> RecipientFunds => Set<RecipientFund>();
    public DbSet<Contributor> Contributors => Set<Contributor>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserScopeAssignment> UserScopeAssignments => Set<UserScopeAssignment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SmsTemplate> SmsTemplates => Set<SmsTemplate>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<CashDrop> CashDrops => Set<CashDrop>();
    public DbSet<EodReport> EodReports => Set<EodReport>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Ignore<BaseDomainEvent>();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CollectionsDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var isTenantEntity = typeof(BaseTenantEntity).IsAssignableFrom(entityType.ClrType);
            var branchIdProperty = entityType.FindProperty("BranchId");
            var hasBranchId = branchIdProperty != null && (branchIdProperty.ClrType == typeof(Guid) || branchIdProperty.ClrType == typeof(Guid?));

            if (isTenantEntity || hasBranchId)
            {
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(CreateCombinedFilter(entityType.ClrType));
            }
        }
        
        // Configure UUIDs and other Postgres specific details
        modelBuilder.HasPostgresExtension("uuid-ossp");
    }

    public Guid? CurrentTenantId => _tenantContext.TenantId;
    public IReadOnlyList<Guid> CurrentTenantIds => _tenantContext.TenantIds;
    public Guid? CurrentBranchId => _branchContext.BranchId;
    public IReadOnlyList<Guid> CurrentBranchIds => _branchContext.BranchIds;
    public bool IsPlatformContext => _tenantContext.IsPlatformContext;
    public bool IsGlobalBranchContext => _branchContext.IsGlobalContext;

    // These properties are accessed by the query filters via 'this'
    public bool IsPlatformFilter => TenancyContextStore.IsPlatform;
    public Guid[] TenantIdsFilter => TenancyContextStore.CurrentTenantIds;
    public bool IsGlobalBranchFilter => TenancyContextStore.IsGlobalBranch;
    public Guid[] BranchIdsFilter => TenancyContextStore.CurrentBranchIds;

    private LambdaExpression CreateCombinedFilter(Type type)
    {
        var parameter = Expression.Parameter(type, "x");
        Expression combinedFilter = Expression.Constant(true);

        // Tenant Filter
        if (typeof(BaseTenantEntity).IsAssignableFrom(type))
        {
            var tenantProperty = Expression.Property(parameter, nameof(BaseTenantEntity.TenantId));
            
            var isPlatform = Expression.Property(Expression.Constant(this), nameof(IsPlatformFilter));
            var tenantIds = Expression.Property(Expression.Constant(this), nameof(TenantIdsFilter));
            var lengthProperty = typeof(Array).GetProperty(nameof(Array.Length))!;
            var tenantIdsIsEmpty = Expression.Equal(Expression.Property(tenantIds, lengthProperty), Expression.Constant(0));
            
            var containsMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(Guid));
            
            var tenantMatch = Expression.Call(null, containsMethod, tenantIds, tenantProperty);
            
            // x => tenantMatch || (isPlatform && tenantIdsIsEmpty)
            var tenantFilter = Expression.OrElse(tenantMatch, Expression.AndAlso(isPlatform, tenantIdsIsEmpty));
            
            combinedFilter = Expression.AndAlso(combinedFilter, tenantFilter);
        }

        // Branch Filter
        var branchPropertyInfo = type.GetProperty("BranchId");
        if (branchPropertyInfo != null)
        {
            var branchProperty = Expression.Property(parameter, branchPropertyInfo);
            
            // x => this.IsGlobalBranchFilter || this.BranchIdsFilter.Length == 0 || this.BranchIdsFilter.Contains(x.BranchId)
            var isGlobalBranch = Expression.Property(Expression.Constant(this), nameof(IsGlobalBranchFilter));
            var branchIds = Expression.Property(Expression.Constant(this), nameof(BranchIdsFilter));

            var lengthProperty = typeof(Array).GetProperty(nameof(Array.Length))!;
            var branchIdsIsEmpty = Expression.Equal(Expression.Property(branchIds, lengthProperty), Expression.Constant(0));
            
            var containsMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(Guid));
            
            Expression branchMatch;
            if (branchPropertyInfo.PropertyType == typeof(Guid?))
            {
                var valueProperty = typeof(Guid?).GetProperty(nameof(Nullable<Guid>.Value))!;
                var hasValueProperty = typeof(Guid?).GetProperty(nameof(Nullable<Guid>.HasValue))!;
                
                var value = Expression.Property(branchProperty, valueProperty);
                var hasValue = Expression.Property(branchProperty, hasValueProperty);
                
                var inList = Expression.Call(null, containsMethod, branchIds, value);
                // Allow if null (org-level) OR if in the list
                branchMatch = Expression.OrElse(Expression.Not(hasValue), inList);
            }
            else
            {
                branchMatch = Expression.Call(null, containsMethod, branchIds, branchProperty);
            }
            
            // Final filter: (IsGlobalBranch OR ListIsEmpty) ? True : branchMatch
            var branchFilter = Expression.OrElse(isGlobalBranch, Expression.OrElse(branchIdsIsEmpty, branchMatch));
            combinedFilter = Expression.AndAlso(combinedFilter, branchFilter);
        }

        return Expression.Lambda(combinedFilter, parameter);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                case EntityState.Modified:
                    entry.Entity.CreatedAt = entry.State == EntityState.Added ? DateTimeOffset.UtcNow : entry.Entity.CreatedAt;
                    entry.Entity.ModifiedAt = entry.State == EntityState.Modified ? DateTimeOffset.UtcNow : entry.Entity.ModifiedAt;

                    if (entry.Entity is BaseTenantEntity tenantEntity)
                    {
                        if (entry.State == EntityState.Added)
                        {
                            if (tenantEntity.TenantId == Guid.Empty)
                            {
                                // Try to fill TenantId from context
                                if (_tenantContext.TenantId.HasValue)
                                {
                                    tenantEntity.TenantId = _tenantContext.TenantId.Value;
                                }
                                else if (!IsPlatformContext)
                                {
                                    // Entity was not given an explicit TenantId and there is no context to derive one
                                    throw new InvalidOperationException(
                                        "Select an organisation context before creating operational data, or set TenantId explicitly.");
                                }
                            }
                            // If TenantId is already set (e.g. by a background worker copying from OutboxMessage), allow it.
                        }
                        else if (entry.State == EntityState.Modified)
                        {
                            // For updates, only enforce tenant context if the entity has no TenantId set
                            // (shouldn't happen in practice, but guards against accidental zero-guid updates)
                            if (tenantEntity.TenantId == Guid.Empty && !_tenantContext.TenantId.HasValue && !IsPlatformContext)
                            {
                                throw new InvalidOperationException(
                                    "Cannot update an entity with no organisation context.");
                            }
                        }

                        // Also handle BranchId if it exists on the entity
                        var branchIdMetadata = entry.Metadata.FindProperty("BranchId");
                        if (branchIdMetadata != null)
                        {
                            var branchIdProperty = entry.Property("BranchId");
                            if (branchIdProperty.CurrentValue == null || (Guid)branchIdProperty.CurrentValue == Guid.Empty)
                            {
                                if (_branchContext.BranchId.HasValue)
                                {
                                    branchIdProperty.CurrentValue = _branchContext.BranchId.Value;
                                }
                                else if (!_branchContext.IsGlobalContext && !IsPlatformContext)
                                {
                                    throw new InvalidOperationException("Select a branch context before creating branch-scoped data.");
                                }
                            }
                        }
                    }
                    break;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
