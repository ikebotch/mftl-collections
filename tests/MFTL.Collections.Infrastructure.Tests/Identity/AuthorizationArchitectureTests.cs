using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Infrastructure.Identity;
using MFTL.Collections.Infrastructure.Identity.Policies;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Infrastructure.Tests.Identity;

public class AuthorizationArchitectureTests
{
    private readonly DbContextOptions<CollectionsDbContext> _options;

    public AuthorizationArchitectureTests()
    {
        _options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: "AuthTests_" + Guid.NewGuid())
            .Options;
    }

    [Fact]
    public async Task PlatformAdmin_ShouldHaveUnrestrictedAccess()
    {
        var context = CreateContext(isPlatformAdmin: true);
        var policy = new PlatformAdminAccessPolicy(context);

        Assert.True(policy.CanManageUsers("global"));
        Assert.True(policy.CanAccessTenant(Guid.NewGuid()));
        
        using var db = new CollectionsDbContext(_options, null!, null!);
        var branches = new List<Branch> { new() { Id = Guid.NewGuid() }, new() { Id = Guid.NewGuid() } }.AsQueryable();
        var filtered = policy.FilterBranches(branches);
        
        Assert.Equal(2, filtered.Count());
    }

    [Fact]
    public async Task OrganisationAdmin_ShouldBeRestrictedToTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var context = CreateContext(tenantIds: [tenantId]);
        var policy = new OrganisationAdminAccessPolicy(context);

        Assert.True(policy.CanAccessTenant(tenantId));
        Assert.False(policy.CanAccessTenant(otherTenantId));

        var branches = new List<Branch> 
        { 
            new() { Id = Guid.NewGuid(), TenantId = tenantId }, 
            new() { Id = Guid.NewGuid(), TenantId = otherTenantId } 
        }.AsQueryable();
        
        var filtered = policy.FilterBranches(branches);
        Assert.Single(filtered);
        Assert.Equal(tenantId, filtered.First().TenantId);
    }

    [Fact]
    public async Task Collector_ShouldOnlySeeOwnCollections()
    {
        var userId = Guid.NewGuid();
        var context = CreateContext(userId: userId);
        var policy = new CollectorAccessPolicy(context);

        var contributions = new List<Contribution>
        {
            new() { Id = Guid.NewGuid(), Receipt = new() { RecordedByUserId = userId } },
            new() { Id = Guid.NewGuid(), Receipt = new() { RecordedByUserId = Guid.NewGuid() } }
        }.AsQueryable();

        var filtered = policy.FilterCollections(contributions);
        Assert.Single(filtered);
        Assert.Equal(userId, filtered.First().Receipt?.RecordedByUserId);
    }

    [Fact]
    public async Task PrivateEvent_ShouldOnlyBeVisibleToAllowedUsers()
    {
        var eventId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var context = CreateContext(branchIds: [branchId]);
        var policy = new BranchAdminAccessPolicy(context);

        var events = new List<Event>
        {
            new() { Id = eventId, BranchId = branchId, IsPrivate = true },
            new() { Id = Guid.NewGuid(), BranchId = Guid.NewGuid(), IsPrivate = true }, // Other private
            new() { Id = Guid.NewGuid(), BranchId = Guid.NewGuid(), IsPrivate = false } // Public
        }.AsQueryable();

        var filtered = policy.FilterEvents(events);
        Assert.Equal(2, filtered.Count()); // Assigned private + any public
        Assert.Contains(filtered, e => e.Id == eventId);
    }

    private AccessContext CreateContext(
        bool isPlatformAdmin = false, 
        IEnumerable<Guid>? tenantIds = null,
        IEnumerable<Guid>? branchIds = null,
        string? collectorId = null,
        Guid? userId = null)
    {
        return new AccessContext(
            userId ?? Guid.NewGuid(),
            "auth0_id",
            "test@test.com",
            [],
            [],
            tenantIds ?? [],
            branchIds ?? [],
            [],
            [],
            collectorId,
            isPlatformAdmin
        );
    }
}
