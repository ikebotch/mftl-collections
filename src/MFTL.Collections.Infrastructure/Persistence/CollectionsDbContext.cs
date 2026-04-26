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

    private LambdaExpression CreateCombinedFilter(Type type, bool hasBranchId)
    {
        var parameter = Expression.Parameter(type, "x");
        
        // Tenant Filter
        Expression tenantFilter;
        if (typeof(BaseTenantEntity).IsAssignableFrom(type))
        {
            var property = Expression.Property(parameter, nameof(BaseTenantEntity.TenantId));
            var context = Expression.Constant(this);
            var tenantContextField = typeof(CollectionsDbContext).GetField("_tenantContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var accessor = Expression.Field(context, tenantContextField!);
            var tenantIdProperty = typeof(ITenantContext).GetProperty(nameof(ITenantContext.TenantId));
            var tenantId = Expression.Property(accessor, tenantIdProperty!);
            var isPlatformContextProperty = typeof(ITenantContext).GetProperty(nameof(ITenantContext.IsPlatformContext));
            var isPlatformContext = Expression.Property(accessor, isPlatformContextProperty!);
            
            var propertyAsNullable = Expression.Convert(property, typeof(Guid?));
            var tenantMatch = Expression.Equal(propertyAsNullable, tenantId);
            tenantFilter = Expression.OrElse(isPlatformContext, tenantMatch);
        }
        else
        {
            tenantFilter = Expression.Constant(true);
        }

        // Branch Filter
        if (hasBranchId)
        {
            var branchProperty = Expression.Property(parameter, "BranchId");
            var context = Expression.Constant(this);
            var branchContextField = typeof(CollectionsDbContext).GetField("_branchContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var accessor = Expression.Field(context, branchContextField!);
            var branchIdProperty = typeof(IBranchContext).GetProperty(nameof(IBranchContext.BranchId));
            var branchId = Expression.Property(accessor, branchIdProperty!);
            
            // If branchId in context is NULL, we don't filter by branch (allow all branches for the tenant)
            var branchIdIsNull = Expression.Equal(branchId, Expression.Constant(null, typeof(Guid?)));
            
            Expression branchMatch;
            if (branchProperty.Type == typeof(Guid?))
            {
                branchMatch = Expression.Equal(branchProperty, branchId);
            }
            else
            {
                // For non-nullable Guid properties, we use the value if not null
                // EF Core handles the conversion in the generated SQL
                branchMatch = Expression.Equal(branchProperty, Expression.Convert(branchId, typeof(Guid)));
            }
            
            var branchFilter = Expression.OrElse(branchIdIsNull, branchMatch);
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
