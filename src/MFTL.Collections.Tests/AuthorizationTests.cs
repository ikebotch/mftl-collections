using Moq;
using FluentAssertions;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Infrastructure.Identity;
using MockQueryable.Moq;

namespace MFTL.Collections.Tests;

public class AuthorizationTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly ScopeAccessService _service;

    public AuthorizationTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _service = new ScopeAccessService(_mockContext.Object, _mockCurrentUserService.Object);
    }

    private void SetupUser(string auth0Id, bool isPlatformAdmin, List<UserScopeAssignment> assignments)
    {
        var user = new User
        {
            Auth0Id = auth0Id,
            IsPlatformAdmin = isPlatformAdmin,
            ScopeAssignments = assignments
        };

        var users = new List<User> { user }.BuildMockDbSet<User>();
        _mockContext.Setup(c => c.Users).Returns(users.Object);
        _mockCurrentUserService.Setup(s => s.UserId).Returns(auth0Id);
    }

    private void SetupPermissions(List<RolePermission> rolePermissions)
    {
        var dbSet = rolePermissions.BuildMockDbSet<RolePermission>();
        _mockContext.Setup(c => c.RolePermissions).Returns(dbSet.Object);
    }

    [Fact]
    public async Task HasPermissionAsync_ShouldReturnTrue_ForPlatformAdmin()
    {
        // Arrange
        SetupUser("auth0|admin", true, new List<UserScopeAssignment>());

        // Act
        var result = await _service.HasPermissionAsync("any.permission");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_ShouldReturnTrue_WhenRoleHasExactPermission()
    {
        // Arrange
        SetupUser("auth0|user", false, new List<UserScopeAssignment> 
        { 
            new() { Role = "Manager" } 
        });
        SetupPermissions(new List<RolePermission> 
        { 
            new() { RoleName = "Manager", PermissionKey = Permissions.Donors.View } 
        });

        // Act
        var result = await _service.HasPermissionAsync(Permissions.Donors.View);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_ShouldReturnTrue_WhenRoleHasGlobalWildcard()
    {
        // Arrange
        SetupUser("auth0|user", false, new List<UserScopeAssignment> 
        { 
            new() { Role = "Admin" } 
        });
        SetupPermissions(new List<RolePermission> 
        { 
            new() { RoleName = "Admin", PermissionKey = Permissions.All } 
        });

        // Act
        var result = await _service.HasPermissionAsync("any.permission");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_ShouldReturnTrue_WhenRoleHasModuleWildcard()
    {
        // Arrange
        SetupUser("auth0|user", false, new List<UserScopeAssignment> 
        { 
            new() { Role = "Collector" } 
        });
        SetupPermissions(new List<RolePermission> 
        { 
            new() { RoleName = "Collector", PermissionKey = Permissions.Contributions.All } 
        });

        // Act
        var result = await _service.HasPermissionAsync(Permissions.Contributions.Create);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_ShouldReturnFalse_WhenRoleDoesNotHavePermission()
    {
        // Arrange
        SetupUser("auth0|user", false, new List<UserScopeAssignment> 
        { 
            new() { Role = "Viewer" } 
        });
        SetupPermissions(new List<RolePermission> 
        { 
            new() { RoleName = "Viewer", PermissionKey = Permissions.Donors.View } 
        });

        // Act
        var result = await _service.HasPermissionAsync("donors.manage");

        // Assert
        result.Should().BeFalse();
    }
}
