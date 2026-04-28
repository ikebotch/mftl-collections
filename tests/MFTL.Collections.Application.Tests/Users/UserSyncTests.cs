using FluentAssertions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Webhooks.Auth0.Commands.UserCreated;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace MFTL.Collections.Application.Tests.Users;

public class UserSyncTests
{
    private readonly IApplicationDbContext _dbContext;

    public UserSyncTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TestDbContext(options);
    }

    [Fact]
    public async Task Webhook_ShouldCreateNewUser_WhenUserDoesNotExist()
    {
        // Arrange
        var command = new UserCreatedWebhookCommand("auth0|123", "test@mftl.com", "Test User", false);
        var handler = new UserCreatedWebhookCommandHandler(_dbContext);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Auth0Id == "auth0|123");
        user.Should().NotBeNull();
        user!.Email.Should().Be("test@mftl.com");
        user.InviteStatus.Should().Be(UserInviteStatus.Accepted);
    }

    [Fact]
    public async Task Webhook_ShouldLinkToExistingInvitedUser_ByEmail()
    {
        // Arrange
        var invitedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "invited@mftl.com",
            Name = "Invited User",
            InviteStatus = UserInviteStatus.Pending,
            IsActive = false
        };
        _dbContext.Users.Add(invitedUser);
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        var command = new UserCreatedWebhookCommand("auth0|invited", "invited@mftl.com", "Invited User Updated", false);
        var handler = new UserCreatedWebhookCommandHandler(_dbContext);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var users = await _dbContext.Users.Where(u => u.Email == "invited@mftl.com").ToListAsync();
        users.Should().HaveCount(1, "should link to existing user instead of creating duplicate");
        
        var user = users.First();
        user.Auth0Id.Should().Be("auth0|invited");
        user.InviteStatus.Should().Be(UserInviteStatus.Accepted);
        user.IsActive.Should().BeTrue();
        user.Name.Should().Be("Invited User Updated");
    }

    [Fact]
    public async Task Webhook_ShouldBeIdempotent()
    {
        // Arrange
        var command = new UserCreatedWebhookCommand("auth0|123", "test@mftl.com", "Test User", false);
        var handler = new UserCreatedWebhookCommandHandler(_dbContext);

        // Act
        await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var users = await _dbContext.Users.Where(u => u.Email == "test@mftl.com").ToListAsync();
        users.Should().HaveCount(1);
    }
}

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), IApplicationDbContext
{
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

    public Guid? CurrentTenantId => null;
    public IReadOnlyList<Guid> CurrentTenantIds => Array.Empty<Guid>();
    public Guid? CurrentBranchId => null;
    public IReadOnlyList<Guid> CurrentBranchIds => Array.Empty<Guid>();
    public bool IsPlatformContext => true;
    public bool IsGlobalBranchContext => true;
}
