using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Webhooks.Auth0.Commands.UserCreated;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Infrastructure.Identity.Auth0.Provisioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Moq;
using Xunit;

namespace MFTL.Collections.Infrastructure.Tests.Identity;

public class UserProvisioningTests
{
    private readonly DbContextOptions<CollectionsDbContext> _options;

    public UserProvisioningTests()
    {
        _options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: "UserProvisioning_" + Guid.NewGuid())
            .Options;
    }

    private IUserProvisioningService CreateService(CollectionsDbContext context)
    {
        var auth0ServiceMock = new Mock<IAuth0Service>();
        return new UserProvisioningService(context, auth0ServiceMock.Object, NullLogger<UserProvisioningService>.Instance);
    }

    [Fact]
    public async Task Service_ShouldCreateNewUser_WhenNotExists()
    {
        // Arrange
        using var context = new CollectionsDbContext(_options, null!, null!);
        var service = CreateService(context);

        // Act
        await service.ProvisionUserAsync("auth0|123", "new@test.com", "New User", new List<string> { "Platform Admin" });

        // Assert
        var user = await context.Users.FirstOrDefaultAsync(u => u.Auth0Id == "auth0|123");
        user.Should().NotBeNull();
        user!.Email.Should().Be("new@test.com");
        user.IsPlatformAdmin.Should().BeTrue();
        user.InviteStatus.Should().Be(UserInviteStatus.Accepted);
    }

    [Fact]
    public async Task Service_ShouldLinkExistingPendingUser_ByEmail()
    {
        // Arrange
        using var context = new CollectionsDbContext(_options, null!, null!);
        var existingUser = new User 
        { 
            Id = Guid.NewGuid(), 
            Email = "invited@test.com", 
            Name = "Invited User", 
            InviteStatus = UserInviteStatus.Pending 
        };
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        await service.ProvisionUserAsync("auth0|invited", "invited@test.com", "Invited User", new List<string>());

        // Assert
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == "invited@test.com");
        user.Should().NotBeNull();
        user!.Auth0Id.Should().Be("auth0|invited");
        user.InviteStatus.Should().Be(UserInviteStatus.Accepted);
        context.Users.Count().Should().Be(1); // No duplicate
    }

    [Fact]
    public async Task Service_ShouldBeIdempotent()
    {
        // Arrange
        using var context = new CollectionsDbContext(_options, null!, null!);
        var service = CreateService(context);

        // Act
        await service.ProvisionUserAsync("auth0|123", "test@test.com", "Test User", new List<string>());
        await service.ProvisionUserAsync("auth0|123", "test@test.com", "Test User", new List<string>()); // Second call

        // Assert
        context.Users.Count().Should().Be(1);
    }

    [Fact]
    public async Task Webhook_ShouldCallService()
    {
        // Arrange
        using var context = new CollectionsDbContext(_options, null!, null!);
        var service = CreateService(context);
        var handler = new UserCreatedWebhookCommandHandler(service);
        var command = new UserCreatedWebhookCommand("auth0|webhook", "webhook@test.com", "Webhook User", false);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var user = await context.Users.FirstOrDefaultAsync(u => u.Auth0Id == "auth0|webhook");
        user.Should().NotBeNull();
        user!.Email.Should().Be("webhook@test.com");
    }
}
