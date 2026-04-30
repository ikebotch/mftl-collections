using Moq;
using FluentAssertions;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Infrastructure.Identity;
using MockQueryable.Moq;

using Microsoft.Extensions.Logging;

namespace MFTL.Collections.Tests;

public class AuthorizationTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ILogger<ScopeAccessService>> _mockLogger;
    private readonly ScopeAccessService _service;

    public AuthorizationTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockTenantContext = new Mock<ITenantContext>();
        _mockLogger = new Mock<ILogger<ScopeAccessService>>();
        _service = new ScopeAccessService(
            _mockContext.Object, 
            _mockCurrentUserService.Object, 
            _mockTenantContext.Object,
            _mockLogger.Object);
    }

    private void SetupUser(string auth0Id, bool isPlatformAdmin, List<UserScopeAssignment> assignments)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Auth0Id = auth0Id,
            IsPlatformAdmin = isPlatformAdmin,
            ScopeAssignments = assignments
        };
        
        foreach (var a in assignments)
        {
            a.UserId = user.Id;
            a.User = user;
        }

        var users = new List<User> { user }.BuildMockDbSet<User>();
        _mockContext.Setup(c => c.Users).Returns(users.Object);
        
        var assignmentsDbSet = assignments.BuildMockDbSet<UserScopeAssignment>();
        _mockContext.Setup(c => c.UserScopeAssignments).Returns(assignmentsDbSet.Object);
        
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
            new() { Role = "Manager", ScopeType = ScopeType.Platform } 
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
            new() { Role = "Admin", ScopeType = ScopeType.Platform } 
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
            new() { Role = "Collector", ScopeType = ScopeType.Platform } 
        });
        SetupPermissions(new List<RolePermission> 
        { 
            new() { RoleName = "Collector", PermissionKey = "contributions.*" } 
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
            new() { Role = "Viewer", ScopeType = ScopeType.Platform } 
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

    [Fact]
    public async Task CanAccessAsync_ShouldReturnTrue_ForOrgAdmin_InAssignedTenant()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        SetupUser("auth0|orgadmin", false, new List<UserScopeAssignment> 
        { 
            new() { Role = "Organisation Admin", ScopeType = ScopeType.Tenant, TargetId = tenantA } 
        });
        SetupPermissions(new List<RolePermission> 
        { 
            new() { RoleName = "Organisation Admin", PermissionKey = Permissions.Branches.View },
            new() { RoleName = "Organisation Admin", PermissionKey = Permissions.Contributions.View },
            new() { RoleName = "Organisation Admin", PermissionKey = Permissions.Dashboard.View }
        });

        // Act & Assert
        (await _service.CanAccessAsync(Permissions.Branches.View, tenantA)).Should().BeTrue();
        (await _service.CanAccessAsync(Permissions.Contributions.View, tenantA)).Should().BeTrue();
        (await _service.CanAccessAsync(Permissions.Dashboard.View, tenantA)).Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessAsync_ShouldReturnFalse_ForOrgAdmin_InUnassignedTenant()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        SetupUser("auth0|orgadmin", false, new List<UserScopeAssignment> 
        { 
            new() { Role = "Organisation Admin", ScopeType = ScopeType.Tenant, TargetId = tenantA } 
        });
        SetupPermissions(new List<RolePermission> 
        { 
            new() { RoleName = "Organisation Admin", PermissionKey = Permissions.Branches.View }
        });

        // Act
        var result = await _service.CanAccessAsync(Permissions.Branches.View, tenantB);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessAsync_ShouldReturnTrue_ForOrgAdmin_WithPipeInAuth0Id()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var auth0Id = "google-oauth2|116477927979198470992";
        SetupUser(auth0Id, false, new List<UserScopeAssignment> 
        { 
            new() { Role = "Organisation Admin", ScopeType = ScopeType.Tenant, TargetId = tenantA } 
        });
        SetupPermissions(new List<RolePermission> 
        { 
            new() { RoleName = "Organisation Admin", PermissionKey = Permissions.Branches.View },
            new() { RoleName = "Organisation Admin", PermissionKey = Permissions.Contributions.View },
            new() { RoleName = "Organisation Admin", PermissionKey = Permissions.Dashboard.View }
        });

        // Act & Assert
        (await _service.CanAccessAsync(Permissions.Branches.View, tenantA)).Should().BeTrue();
        (await _service.CanAccessAsync(Permissions.Contributions.View, tenantA)).Should().BeTrue();
        (await _service.CanAccessAsync(Permissions.Dashboard.View, tenantA)).Should().BeTrue();
    }
}
