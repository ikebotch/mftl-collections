using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Users.Queries.GetMe;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Moq;
using Xunit;
using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;

namespace MFTL.Collections.Infrastructure.Tests.Identity;

public class GetMeTests
{
    private readonly DbContextOptions<CollectionsDbContext> _options;

    public GetMeTests()
    {
        _options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: "GetMe_" + Guid.NewGuid())
            .Options;
    }

    [Fact]
    public async Task Handle_ShouldReturnActiveState_WhenUserHasScopes()
    {
        // Arrange
        using var context = new CollectionsDbContext(_options, null!, null!);
        var userId = Guid.NewGuid();
        var auth0Id = "auth0|123";
        
        var user = new User 
        { 
            Id = userId, 
            Auth0Id = auth0Id, 
            Email = "test@test.com", 
            Name = "Test User",
            IsActive = true
        };
        user.ScopeAssignments.Add(new UserScopeAssignment 
        { 
            Role = "Collector", 
            ScopeType = ScopeType.Platform 
        });
        
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(x => x.UserId).Returns(auth0Id);
        currentUserServiceMock.Setup(x => x.Roles).Returns(new List<string> { "Collector" });

        var provisioningMock = new Mock<IUserProvisioningService>();
        var permissionEvaluatorMock = new Mock<IPermissionEvaluator>();
        permissionEvaluatorMock.Setup(x => x.GetEffectivePermissionsAsync()).ReturnsAsync(new List<string>());

        var handler = new GetMeQueryHandler(context, currentUserServiceMock.Object, provisioningMock.Object, permissionEvaluatorMock.Object, NullLogger<GetMeQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetMeQuery(), CancellationToken.None);

        // Assert
        result.AccessState.Should().Be("active");
    }

    [Fact]
    public async Task Handle_ShouldReturnPendingAccessState_WhenUserHasNoScopesAndNotAdmin()
    {
        // Arrange
        using var context = new CollectionsDbContext(_options, null!, null!);
        var auth0Id = "auth0|no-scopes";
        
        var user = new User 
        { 
            Id = Guid.NewGuid(), 
            Auth0Id = auth0Id, 
            Email = "no@scopes.com", 
            Name = "No Scopes",
            IsActive = true,
            IsPlatformAdmin = false
        };
        
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(x => x.UserId).Returns(auth0Id);
        currentUserServiceMock.Setup(x => x.Roles).Returns(new List<string>());

        var provisioningMock = new Mock<IUserProvisioningService>();
        var permissionEvaluatorMock = new Mock<IPermissionEvaluator>();
        permissionEvaluatorMock.Setup(x => x.GetEffectivePermissionsAsync()).ReturnsAsync(new List<string>());

        var handler = new GetMeQueryHandler(context, currentUserServiceMock.Object, provisioningMock.Object, permissionEvaluatorMock.Object, NullLogger<GetMeQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetMeQuery(), CancellationToken.None);

        // Assert
        result.AccessState.Should().Be("pending-access");
    }

    [Fact]
    public async Task Handle_ShouldReturnActiveState_WhenUserIsPlatformAdmin()
    {
        // Arrange
        using var context = new CollectionsDbContext(_options, null!, null!);
        var auth0Id = "auth0|admin";
        
        var user = new User 
        { 
            Id = Guid.NewGuid(), 
            Auth0Id = auth0Id, 
            Email = "admin@test.com", 
            Name = "Admin User",
            IsActive = true,
            IsPlatformAdmin = true
        };
        
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(x => x.UserId).Returns(auth0Id);
        currentUserServiceMock.Setup(x => x.Roles).Returns(new List<string> { "Platform Admin" });

        var provisioningMock = new Mock<IUserProvisioningService>();
        var permissionEvaluatorMock = new Mock<IPermissionEvaluator>();
        permissionEvaluatorMock.Setup(x => x.GetEffectivePermissionsAsync()).ReturnsAsync(new List<string>());

        var handler = new GetMeQueryHandler(context, currentUserServiceMock.Object, provisioningMock.Object, permissionEvaluatorMock.Object, NullLogger<GetMeQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetMeQuery(), CancellationToken.None);

        // Assert
        result.AccessState.Should().Be("active");
    }

    [Fact]
    public async Task Handle_ShouldReturnSuspendedState_WhenUserIsSuspended()
    {
        // Arrange
        using var context = new CollectionsDbContext(_options, null!, null!);
        var auth0Id = "auth0|suspended";
        
        var user = new User 
        { 
            Id = Guid.NewGuid(), 
            Auth0Id = auth0Id, 
            Email = "suspended@test.com", 
            Name = "Suspended User",
            IsActive = true,
            IsSuspended = true
        };
        
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(x => x.UserId).Returns(auth0Id);

        var provisioningMock = new Mock<IUserProvisioningService>();
        var permissionEvaluatorMock = new Mock<IPermissionEvaluator>();
        permissionEvaluatorMock.Setup(x => x.GetEffectivePermissionsAsync()).ReturnsAsync(new List<string>());

        var handler = new GetMeQueryHandler(context, currentUserServiceMock.Object, provisioningMock.Object, permissionEvaluatorMock.Object, NullLogger<GetMeQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetMeQuery(), CancellationToken.None);

        // Assert
        result.AccessState.Should().Be("suspended");
    }
}
