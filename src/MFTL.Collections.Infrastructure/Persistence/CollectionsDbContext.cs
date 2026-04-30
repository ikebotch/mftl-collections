using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Common;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Application.Common.Interfaces;
using System.Linq.Expressions;

namespace MFTL.Collections.Infrastructure.Persistence;

public sealed class CollectionsDbContext(DbContextOptions<CollectionsDbContext> options, ITenantContext tenantContext) : DbContext(options), IApplicationDbContext
{
    private readonly ITenantContext _tenantContext = tenantContext;

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
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedExternalPaymentCallback> ProcessedExternalPaymentCallbacks => Set<ProcessedExternalPaymentCallback>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CollectionsDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var parameter = Expression.Parameter(clrType, "e");
            var context = Expression.Constant(this);
            var tenantContextField = typeof(CollectionsDbContext).GetField("_tenantContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var accessor = Expression.Field(context, tenantContextField!);
            
            Expression? filter = null;

            // 1. Tenant Filter
            if (typeof(BaseTenantEntity).IsAssignableFrom(clrType))
            {
                var tenantIdProp = Expression.Property(parameter, nameof(BaseTenantEntity.TenantId));
                var ctxTenantId = Expression.Property(accessor, nameof(ITenantContext.TenantId));
                var ctxAllowedTenants = Expression.Property(accessor, nameof(ITenantContext.AllowedTenantIds));
                var ctxAllowedBranches = Expression.Property(accessor, nameof(ITenantContext.AllowedBranchIds));
                var isPlatform = Expression.Property(accessor, nameof(ITenantContext.IsPlatformContext));

                // (IsPlatformContext || (Context.TenantId != null ? TenantId == Context.TenantId : Context.AllowedTenantIds.Contains(TenantId)))
                var hasTenantId = Expression.Property(ctxTenantId, "HasValue");
                var tenantIdValue = Expression.Property(ctxTenantId, "Value");
                
                var exactTenantMatch = Expression.Equal(tenantIdProp, tenantIdValue);
                var allowedTenantsContains = Expression.Call(
                    typeof(Enumerable), 
                    "Contains", 
                    new[] { typeof(Guid) }, 
                    ctxAllowedTenants, 
                    tenantIdProp);

                var tenantFilter = Expression.OrElse(
                    isPlatform,
                    Expression.Condition(
                        hasTenantId,
                        exactTenantMatch,
                        allowedTenantsContains
                    )
                );
                filter = tenantFilter;

                // 2. Branch Filter (if applicable)
                if (typeof(IBranchEntity).IsAssignableFrom(clrType))
                {
                    var branchIdProp = Expression.Property(parameter, nameof(IBranchEntity.BranchId));
                    // (IsPlatformContext || Context.AllowedBranchIds.Contains(BranchId))
                    var allowedBranchesContains = Expression.Call(
                        typeof(Enumerable), 
                        "Contains", 
                        new[] { typeof(Guid) }, 
                        ctxAllowedBranches, 
                        branchIdProp);
                    
                    var branchFilter = Expression.OrElse(isPlatform, allowedBranchesContains);
                    
                    filter = Expression.AndAlso(filter, branchFilter);
                }
                else if (typeof(IOptionalBranchEntity).IsAssignableFrom(clrType))
                {
                    var branchIdProp = Expression.Property(parameter, nameof(IOptionalBranchEntity.BranchId));
                    // (IsPlatformContext || BranchId == null || Context.AllowedBranchIds.Contains(BranchId))
                    var branchIsNull = Expression.Equal(branchIdProp, Expression.Constant(null, typeof(Guid?)));
                    var allowedBranchesContains = Expression.Call(
                        typeof(Enumerable), 
                        "Contains", 
                        new[] { typeof(Guid) }, 
                        ctxAllowedBranches, 
                        Expression.Property(branchIdProp, "Value"));
                    
                    // Only call Contains if BranchId has value
                    var branchFilter = Expression.OrElse(
                        Expression.OrElse(isPlatform, branchIsNull),
                        Expression.AndAlso(Expression.Property(branchIdProp, "HasValue"), allowedBranchesContains)
                    );
                    
                    filter = Expression.AndAlso(filter, branchFilter);
                }
                else if (clrType == typeof(Branch))
                {
                    // Special case for Branch itself: Id must be in allowed list
                    var idProp = Expression.Property(parameter, nameof(Branch.Id));
                    
                    var allowedBranchesContains = Expression.Call(
                        typeof(Enumerable), 
                        "Contains", 
                        new[] { typeof(Guid) }, 
                        ctxAllowedBranches, 
                        idProp);
                    
                    var branchFilter = Expression.OrElse(isPlatform, allowedBranchesContains);
                    filter = Expression.AndAlso(filter, branchFilter);
                }
            }
            else if (clrType == typeof(UserScopeAssignment))
            {
                // 3. UserScopeAssignment Filter: Must belong to allowed tenants or branches
                var scopeTypeProp = Expression.Property(parameter, nameof(UserScopeAssignment.ScopeType));
                var targetIdProp = Expression.Property(parameter, nameof(UserScopeAssignment.TargetId));
                var isPlatform = Expression.Property(accessor, nameof(ITenantContext.IsPlatformContext));
                var ctxAllowedTenants = Expression.Property(accessor, nameof(ITenantContext.AllowedTenantIds));
                var ctxAllowedBranches = Expression.Property(accessor, nameof(ITenantContext.AllowedBranchIds));

                var tenantScope = Expression.Constant(Domain.Entities.ScopeType.Tenant);
                var branchScope = Expression.Constant(Domain.Entities.ScopeType.Branch);

                var isTenantScope = Expression.Equal(scopeTypeProp, tenantScope);
                var isBranchScope = Expression.Equal(scopeTypeProp, branchScope);

                var targetIdValue = Expression.Property(targetIdProp, "Value");
                var hasValue = Expression.Property(targetIdProp, "HasValue");

                var tenantMatch = Expression.AndAlso(
                    isTenantScope, 
                    Expression.AndAlso(
                        hasValue,
                        Expression.Call(typeof(Enumerable), "Contains", new[] { typeof(Guid) }, ctxAllowedTenants, targetIdValue)
                    ));

                var branchMatch = Expression.AndAlso(
                    isBranchScope, 
                    Expression.AndAlso(
                        hasValue,
                        Expression.Call(typeof(Enumerable), "Contains", new[] { typeof(Guid) }, ctxAllowedBranches, targetIdValue)
                    ));

                filter = Expression.OrElse(isPlatform, Expression.OrElse(tenantMatch, branchMatch));
            }

            if (filter != null)
            {
                modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(filter, parameter));
            }
        }
        
        // Configure UUIDs and other Postgres specific details
        modelBuilder.HasPostgresExtension("uuid-ossp");
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
                        if (_tenantContext.IsSystemContext || _tenantContext.IsPlatformContext)
                        {
                            // System/Platform context can bypass context checks and use preset IDs
                            if (tenantEntity.TenantId == Guid.Empty && _tenantContext.TenantId.HasValue)
                            {
                                tenantEntity.TenantId = _tenantContext.TenantId.Value;
                            }
                        }
                        else
                        {
                            // Normal request: must match context
                            if (!_tenantContext.TenantId.HasValue)
                            {
                                throw new InvalidOperationException("Tenant context is required for non-system requests.");
                            }

                            if (tenantEntity.TenantId != Guid.Empty && tenantEntity.TenantId != _tenantContext.TenantId.Value)
                            {
                                throw new UnauthorizedAccessException("Cannot spoof TenantId.");
                            }

                            tenantEntity.TenantId = _tenantContext.TenantId.Value;
                        }
                    }

                    if (entry.Entity is IBranchEntity branchEntity && !_tenantContext.IsSystemContext && !_tenantContext.IsPlatformContext)
                    {
                        if (branchEntity.BranchId != Guid.Empty && _tenantContext.BranchId.HasValue && branchEntity.BranchId != _tenantContext.BranchId.Value)
                        {
                            throw new UnauthorizedAccessException("Cannot spoof BranchId.");
                        }
                        
                        if (branchEntity.BranchId == Guid.Empty && _tenantContext.BranchId.HasValue)
                        {
                            branchEntity.BranchId = _tenantContext.BranchId.Value;
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
