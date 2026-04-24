using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace MFTL.Collections.Infrastructure.Persistence;

public class CollectionsDbContextFactory : IDesignTimeDbContextFactory<CollectionsDbContext>
{
    public CollectionsDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        if (basePath.EndsWith("MFTL.Collections.Api"))
        {
            // Already in Api
        }
        else if (Directory.Exists(Path.Combine(basePath, "src/MFTL.Collections.Api")))
        {
            basePath = Path.Combine(basePath, "src/MFTL.Collections.Api");
        }
        else if (Directory.Exists(Path.Combine(basePath, "../MFTL.Collections.Api")))
        {
            basePath = Path.Combine(basePath, "../MFTL.Collections.Api");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("local.settings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<CollectionsDbContext>();
        var connectionString = configuration.GetSection("Values")["Database:ConnectionString"];

        if (string.IsNullOrEmpty(connectionString))
        {
            // Fallback for design-time if local.settings.json is not found or empty
            connectionString = "Host=localhost;Database=mftl-collections;Username=postgres;Password=postgres";
        }

        optionsBuilder.UseNpgsql(connectionString);

        return new CollectionsDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }
}

public class DesignTimeTenantContext : Application.Common.Interfaces.ITenantContext
{
    public Guid? TenantId => Guid.Empty;
    public string? TenantIdentifier => "design-time";
    public bool IsPlatformContext => true;
    public void UseTenant(Guid tenantId, string? identifier = null) { }
    public void UsePlatformContext() { }
    public void Clear() { }
}
