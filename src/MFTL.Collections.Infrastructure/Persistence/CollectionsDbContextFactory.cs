using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Infrastructure.Persistence;

public class CollectionsDbContextFactory : IDesignTimeDbContextFactory<CollectionsDbContext>
{
    public CollectionsDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "MFTL.Collections.Api");
        
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("local.settings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetValue<string>("Database:ConnectionString") 
                               ?? configuration.GetValue<string>("Values:Database:ConnectionString")
                               ?? Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Could not find connection string. Please set 'Database:ConnectionString' in local.settings.json or 'DATABASE_CONNECTION_STRING' environment variable.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<CollectionsDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new CollectionsDbContext(optionsBuilder.Options, new DesignTimeTenantContext(), new DesignTimeBranchContext());
    }

    private class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public IReadOnlyList<Guid> TenantIds => Array.Empty<Guid>();
        public string? TenantIdentifier => null;
        public bool IsPlatformContext => true;
    }

    private class DesignTimeBranchContext : IBranchContext
    {
        public Guid? BranchId => null;
        public IReadOnlyList<Guid> BranchIds => Array.Empty<Guid>();
        public void UseBranch(Guid branchId) { }
        public void UseBranches(IEnumerable<Guid> branchIds) { }
        public void Clear() { }
    }
}
