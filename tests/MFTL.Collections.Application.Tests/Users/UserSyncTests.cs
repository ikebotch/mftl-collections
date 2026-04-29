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
    public async Task Webhook_ShouldCallProvisioningService()
    {
        // Arrange
        var provisioningService = Substitute.For<IUserProvisioningService>();
        var command = new UserCreatedWebhookCommand("auth0|123", "test@mftl.com", "Test User", false);
        var handler = new UserCreatedWebhookCommandHandler(provisioningService);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        await provisioningService.Received(1).ProvisionUserAsync(
            "auth0|123", 
            "test@mftl.com", 
            "Test User", 
            Arg.Any<List<string>>(), 
            null,
            null,
            null,
            null,
            Arg.Any<CancellationToken>());
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
    public DbSet<SmsTemplate> SmsTemplates => Set<SmsTemplate>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();

    public Guid? CurrentTenantId => null;
    public IReadOnlyList<Guid> CurrentTenantIds => Array.Empty<Guid>();
    public Guid? CurrentBranchId => null;
    public IReadOnlyList<Guid> CurrentBranchIds => Array.Empty<Guid>();
    public bool IsPlatformContext => true;
    public bool IsGlobalBranchContext => true;
}
