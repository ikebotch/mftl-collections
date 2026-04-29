using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Infrastructure.Identity.Auth0.Provisioning;
using MFTL.Collections.Infrastructure.Persistence;
using Moq;
using Xunit;

namespace MFTL.Collections.Infrastructure.Tests.Identity;

public class UserProvisioningServiceTests
{
    private readonly Mock<IAuth0Service> _auth0ServiceMock;
    private readonly ILogger<UserProvisioningService> _logger;

    public UserProvisioningServiceTests()
    {
        _auth0ServiceMock = new Mock<IAuth0Service>();
        _logger = NullLogger<UserProvisioningService>.Instance;
    }

    private CollectionsDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new CollectionsDbContext(options, new TestTenantContext(), new TestBranchContext());
    }

    [Fact]
    public async Task GoogleUser_WithMissingEmailClaim_ButUserInfoEmail_UpdatesLocalDb()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var auth0Id = "google-oauth2|123456";
        var accessToken = "test-token";
        
        _auth0ServiceMock.Setup(s => s.GetUserInfoAsync(accessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(("real-email@gmail.com", "Real Name", "realname", "pic-url"));

        await using var dbContext = CreateDbContext(dbName);
        var service = new UserProvisioningService(dbContext, _auth0ServiceMock.Object, _logger);

        // Act
        await service.ProvisionUserAsync(auth0Id, "", "", new List<string>(), accessToken);

        // Assert
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Auth0Id == auth0Id);
        user.Should().NotBeNull();
        user!.Email.Should().Be("real-email@gmail.com");
        user.Name.Should().Be("Real Name");
    }

    [Fact]
    public async Task ExistingUnprovisionedEmail_IsReplacedWithRealEmail()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var auth0Id = "google-oauth2|789";
        var dummyEmail = $"{auth0Id}@unprovisioned.mftl";
        
        await using var seedContext = CreateDbContext(dbName);
        seedContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Auth0Id = auth0Id,
            Email = dummyEmail,
            Name = auth0Id,
            IsActive = true,
            Pin = "1234"
        });
        await seedContext.SaveChangesAsync();

        await using var dbContext = CreateDbContext(dbName);
        var service = new UserProvisioningService(dbContext, _auth0ServiceMock.Object, _logger);

        // Act
        await service.ProvisionUserAsync(auth0Id, "real-user@example.com", "Real User", new List<string>());

        // Assert
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Auth0Id == auth0Id);
        user!.Email.Should().Be("real-user@example.com");
        user.Name.Should().Be("Real User");
    }

    [Fact]
    public async Task ScopeAssignments_RemainAfterProfileUpdate()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var auth0Id = "auth0|scope-test";
        
        await using var seedContext = CreateDbContext(dbName);
        var user = new User { Auth0Id = auth0Id, Email = "old@test.com", Name = "Old Name", IsActive = true, Pin = "1234" };
        seedContext.Users.Add(user);
        seedContext.UserScopeAssignments.Add(new UserScopeAssignment { User = user, Role = "Collector", ScopeType = ScopeType.Platform });
        await seedContext.SaveChangesAsync();

        await using var dbContext = CreateDbContext(dbName);
        var service = new UserProvisioningService(dbContext, _auth0ServiceMock.Object, _logger);

        // Act
        await service.ProvisionUserAsync(auth0Id, "new@test.com", "New Name", new List<string>());

        // Assert
        var updatedUser = await dbContext.Users.Include(u => u.ScopeAssignments).FirstOrDefaultAsync(u => u.Auth0Id == auth0Id);
        updatedUser!.Email.Should().Be("new@test.com");
        updatedUser.ScopeAssignments.Should().HaveCount(1);
        updatedUser.ScopeAssignments.First().Role.Should().Be("Collector");
    }

    [Fact]
    public async Task LastLoginAt_UpdatesOnEveryCall()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var auth0Id = "auth0|login-test";
        var initialLogin = DateTimeOffset.UtcNow.AddDays(-1);
        
        await using var seedContext = CreateDbContext(dbName);
        seedContext.Users.Add(new User { Auth0Id = auth0Id, Email = "test@test.com", Name = "Test", IsActive = true, Pin = "1234", LastLoginAt = initialLogin });
        await seedContext.SaveChangesAsync();

        await using var dbContext = CreateDbContext(dbName);
        var service = new UserProvisioningService(dbContext, _auth0ServiceMock.Object, _logger);

        // Act
        await service.ProvisionUserAsync(auth0Id, "test@test.com", "Test", new List<string>());

        // Assert
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Auth0Id == auth0Id);
        user!.LastLoginAt.Should().BeAfter(initialLogin);
    }

    private class TestTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public IReadOnlyList<Guid> TenantIds => Array.Empty<Guid>();
        public string? TenantIdentifier => null;
        public bool IsPlatformContext => true;
    }

    private class TestBranchContext : IBranchContext
    {
        public Guid? BranchId => null;
        public IReadOnlyList<Guid> BranchIds => Array.Empty<Guid>();
        public bool IsGlobalContext => true;
        public void UseBranch(Guid branchId) { }
        public void UseBranches(IEnumerable<Guid> branchIds) { }
        public void UseGlobalContext() { }
        public void Clear() { }
    }
}
