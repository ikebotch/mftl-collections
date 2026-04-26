using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Common;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Application.Common.Interfaces;
using System.Linq.Expressions;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CollectionsDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var isTenantEntity = typeof(BaseTenantEntity).IsAssignableFrom(entityType.ClrType);
            var branchIdProperty = entityType.FindProperty("BranchId");
            var hasBranchId = branchIdProperty != null && (branchIdProperty.ClrType == typeof(Guid) || branchIdProperty.ClrType == typeof(Guid?));

            if (isTenantEntity || hasBranchId)
            {
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(CreateCombinedFilter(entityType.ClrType, hasBranchId));
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

    private LambdaExpression CreateCombinedFilter(Type type, bool hasBranchId)
    {
        var parameter = Expression.Parameter(type, "x");
        
        // Tenant Filter
        Expression tenantFilter;
        if (typeof(BaseTenantEntity).IsAssignableFrom(type))
        {
            var property = Expression.Property(parameter, nameof(BaseTenantEntity.TenantId));
            
            var tenantIdsProperty = typeof(CollectionsDbContext).GetProperty(nameof(CurrentTenantIds));
            var tenantIds = Expression.Property(Expression.Constant(this), tenantIdsProperty!);
            
            var isPlatformProperty = typeof(CollectionsDbContext).GetProperty(nameof(IsPlatformContext));
            var isPlatform = Expression.Property(Expression.Constant(this), isPlatformProperty!);
            
            // Contains logic: x => IsPlatform || CurrentTenantIds.Contains(x.TenantId)
            var containsMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(Guid));
            
            var tenantMatch = Expression.Call(null, containsMethod, tenantIds, property);
            tenantFilter = Expression.OrElse(isPlatform, tenantMatch);
        }
        else
        {
            tenantFilter = Expression.Constant(true);
        }

        // Branch Filter
        if (hasBranchId)
        {
            var branchProperty = Expression.Property(parameter, "BranchId");
            
            var branchIdsProperty = typeof(CollectionsDbContext).GetProperty(nameof(CurrentBranchIds));
            var branchIds = Expression.Property(Expression.Constant(this), branchIdsProperty!);
            
            // If branchIds in context is EMPTY, we don't filter by branch (allow all branches for the tenant)
            // Use IReadOnlyCollection because Count is defined there, and Reflection doesn't find inherited properties on interfaces
            var countProperty = typeof(IReadOnlyCollection<Guid>).GetProperty(nameof(IReadOnlyCollection<Guid>.Count));
            var branchIdsIsEmpty = Expression.Equal(Expression.Property(branchIds, countProperty!), Expression.Constant(0));
            
            // Contains logic: x => BranchIds.IsEmpty() || CurrentBranchIds.Contains(x.BranchId)
            var containsMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(Guid));
            
            Expression branchMatch;
            if (branchProperty.Type == typeof(Guid?))
            {
                // For nullable Guid properties, we check if they have a value and if that value is in the list
                var hasValueProperty = typeof(Guid?).GetProperty(nameof(Nullable<Guid>.HasValue));
                var valueProperty = typeof(Guid?).GetProperty(nameof(Nullable<Guid>.Value));
                
                var hasValue = Expression.Property(branchProperty, hasValueProperty!);
                var value = Expression.Property(branchProperty, valueProperty!);
                
                var inList = Expression.Call(null, containsMethod, branchIds, value);
                branchMatch = Expression.AndAlso(hasValue, inList);
            }
            else
            {
                branchMatch = Expression.Call(null, containsMethod, branchIds, branchProperty);
            }
            
            var branchFilter = Expression.OrElse(branchIdsIsEmpty, branchMatch);
            return Expression.Lambda(Expression.AndAlso(tenantFilter, branchFilter), parameter);
        }

        return Expression.Lambda(tenantFilter, parameter);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
                    if (entry.Entity is BaseTenantEntity tenantEntity)
                    {
                        if (!_tenantContext.TenantId.HasValue)
                        {
                            throw new InvalidOperationException("Tenant context is required when creating tenant-owned entities.");
                        }

                        if (tenantEntity.TenantId == Guid.Empty)
                        {
                            tenantEntity.TenantId = _tenantContext.TenantId.Value;
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
                            }
                        }
                    }
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedAt = DateTimeOffset.UtcNow;
                    break;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
