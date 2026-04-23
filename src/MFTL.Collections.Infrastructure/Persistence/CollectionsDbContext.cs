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
    public DbSet<Event> Events => Set<Event>();
    public DbSet<RecipientFund> RecipientFunds => Set<RecipientFund>();
    public DbSet<Contributor> Contributors => Set<Contributor>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserScopeAssignment> UserScopeAssignments => Set<UserScopeAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CollectionsDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseTenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(CreateTenantFilter(entityType.ClrType));
            }
        }
        
        // Configure UUIDs and other Postgres specific details
        modelBuilder.HasPostgresExtension("uuid-ossp");
    }

    private LambdaExpression CreateTenantFilter(Type type)
    {
        var parameter = Expression.Parameter(type, "x");
        var property = Expression.Property(parameter, nameof(BaseTenantEntity.TenantId));
        
        var context = Expression.Constant(this);
        var tenantContextField = typeof(CollectionsDbContext).GetField("_tenantContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var accessor = Expression.Field(context, tenantContextField!);
        var tenantIdProperty = typeof(ITenantContext).GetProperty(nameof(ITenantContext.TenantId));
        var tenantId = Expression.Property(accessor, tenantIdProperty!);
        
        // Handle nullable Guid comparison
        var equality = Expression.Equal(property, Expression.Convert(tenantId, typeof(Guid)));
        return Expression.Lambda(equality, parameter);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
                    if (entry.Entity is BaseTenantEntity tenantEntity && tenantEntity.TenantId == Guid.Empty && _tenantContext.TenantId.HasValue)
                    {
                        tenantEntity.TenantId = _tenantContext.TenantId.Value;
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
