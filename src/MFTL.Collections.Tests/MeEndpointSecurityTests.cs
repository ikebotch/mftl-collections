using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Api.Functions.Users;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Infrastructure.Tenancy;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using Moq;
using Xunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MFTL.Collections.Application.Features.Users.Queries.GetUserById;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Tests;

public class MeEndpointSecurityTests : IDisposable
{
    private readonly CollectionsDbContext _dbContext;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly UserFunctions _functions;

    public MeEndpointSecurityTests()
    {
        var services = new ServiceCollection();
        
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _tenantContextMock = new Mock<ITenantContext>();
        _dbContext = new CollectionsDbContext(options, _tenantContextMock.Object, _currentUserServiceMock.Object);
        
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetUserByIdQuery).Assembly));
        services.AddAuthentication().AddCookie(); // Add dummy authentication
        services.AddLogging();
        services.AddSingleton<IApplicationDbContext>(_dbContext);
        services.AddSingleton(_currentUserServiceMock.Object);
        services.AddSingleton(_tenantContextMock.Object);
        
        _serviceProvider = services.BuildServiceProvider();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var scopeService = new Mock<IScopeAccessService>();

        _functions = new UserFunctions(
            mediator,
            _dbContext,
            _currentUserServiceMock.Object,
            scopeService.Object,
            _tenantContextMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _serviceProvider.Dispose();
    }

    private User CreateUser(string auth0Id, bool isPlatformAdmin = false)
    {
        var user = new User { Auth0Id = auth0Id, Name = "Test User", Email = "test@test.com", IsPlatformAdmin = isPlatformAdmin };
        _dbContext.Users.Add(user);
        _dbContext.SaveChanges();
        return user;
    }

    [Fact]
    public async Task GetMe_WithOneTenantAdminAssignment_ShouldBootstrapTenant()
    {
        // Arrange
        var auth0Id = "auth0|single-tenant-admin";
        var user = CreateUser(auth0Id);
        var tenantId = Guid.NewGuid();
        
        _dbContext.UserScopeAssignments.Add(new UserScopeAssignment 
        { 
            UserId = user.Id, 
            ScopeType = ScopeType.Tenant, 
            TargetId = tenantId, 
            Role = AppRoles.OrganisationAdmin
        });
        _dbContext.SaveChanges();

        // Seed with canonical name
        _dbContext.RolePermissions.Add(new RolePermission { RoleName = AppRoles.OrganisationAdmin, PermissionKey = "tenant.admin.perm" });
        _dbContext.SaveChanges();

        _currentUserServiceMock.Setup(u => u.UserId).Returns(auth0Id);
        _tenantContextMock.Setup(t => t.TenantId).Returns((Guid?)null); 

        var context = new DefaultHttpContext { RequestServices = _serviceProvider };
        var request = context.Request;

        // Act
        var result = await _functions.GetMe(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<UserDetailDto>>(okResult.Value);
        Assert.Contains(AppRoles.OrganisationAdmin, apiResponse.Data.EffectiveRoleKeys);
        Assert.Contains("Organisation Admin", apiResponse.Data.EffectiveRoles);
        Assert.Contains("tenant.admin.perm", apiResponse.Data.Permissions);
        Assert.Equal(tenantId, apiResponse.Data.ActiveTenantId);
    }

    [Fact]
    public async Task GetMe_WithMultipleTenantAssignments_ShouldNotUnionPermissions()
    {
        // Arrange
        var auth0Id = "auth0|multi-tenant";
        var user = CreateUser(auth0Id);
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();
        
        _dbContext.UserScopeAssignments.Add(new UserScopeAssignment { UserId = user.Id, ScopeType = ScopeType.Tenant, TargetId = tenant1Id, Role = "Admin1" });
        _dbContext.UserScopeAssignments.Add(new UserScopeAssignment { UserId = user.Id, ScopeType = ScopeType.Tenant, TargetId = tenant2Id, Role = "Admin2" });
        _dbContext.SaveChanges();

        _currentUserServiceMock.Setup(u => u.UserId).Returns(auth0Id);
        _tenantContextMock.Setup(t => t.TenantId).Returns((Guid?)null); // Missing X-Tenant-Id

        var context = new DefaultHttpContext { RequestServices = _serviceProvider };
        var request = context.Request;

        // Act
        var result = await _functions.GetMe(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<UserDetailDto>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Empty(apiResponse.Data.EffectiveRoles);
        Assert.Empty(apiResponse.Data.Permissions);
        Assert.Equal(2, apiResponse.Data.ScopeAssignments.Count());
    }

    [Fact]
    public async Task GetMe_WithInvalidTenant_ShouldReturnEmptyPermissionsNot500()
    {
        // Arrange
        var auth0Id = "auth0|invalid-tenant";
        var user = CreateUser(auth0Id);
        var tenantId = Guid.NewGuid();
        
        _dbContext.UserScopeAssignments.Add(new UserScopeAssignment { UserId = user.Id, ScopeType = ScopeType.Tenant, TargetId = tenantId, Role = "Admin" });
        _dbContext.SaveChanges();

        _currentUserServiceMock.Setup(u => u.UserId).Returns(auth0Id);
        _tenantContextMock.Setup(t => t.TenantId).Returns(Guid.NewGuid()); // Different tenant than assigned

        var context = new DefaultHttpContext { RequestServices = _serviceProvider };
        var request = context.Request;

        // Act
        var result = await _functions.GetMe(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<UserDetailDto>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);
        Assert.Empty(apiResponse.Data.EffectiveRoles);
        Assert.Empty(apiResponse.Data.Permissions);
    }
    [Fact]
    public async Task GetMe_WithCollectorEventScope_ShouldBootstrapTenant()
    {
        // Arrange
        var auth0Id = "auth0|event-collector";
        var user = CreateUser(auth0Id);
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        _dbContext.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Tenant", Identifier = "test" });
        _dbContext.Events.Add(new Event { Id = eventId, Title = "Test Event", TenantId = tenantId });
        
        _dbContext.UserScopeAssignments.Add(new UserScopeAssignment 
        { 
            UserId = user.Id, 
            ScopeType = ScopeType.Event, 
            TargetId = eventId, 
            Role = AppRoles.Collector 
        });
        _dbContext.SaveChanges();

        _dbContext.RolePermissions.Add(new RolePermission { RoleName = AppRoles.Collector, PermissionKey = "collector.perm" });
        _dbContext.SaveChanges();

        _currentUserServiceMock.Setup(u => u.UserId).Returns(auth0Id);
        _tenantContextMock.Setup(t => t.TenantId).Returns((Guid?)null); 

        var context = new DefaultHttpContext { RequestServices = _serviceProvider };
        var request = context.Request;

        // Act
        var result = await _functions.GetMe(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<UserDetailDto>>(okResult.Value);
        Assert.Contains(AppRoles.Collector, apiResponse.Data.EffectiveRoleKeys);
        Assert.Contains("Collector", apiResponse.Data.EffectiveRoles);
        Assert.Contains("collector.perm", apiResponse.Data.Permissions);
        Assert.Equal(tenantId, apiResponse.Data.ActiveTenantId);
    }

    [Fact]
    public async Task GetMe_WithMultipleTenantAssignments_AndXTenantId_ShouldReturnCorrectRole()
    {
        // Arrange
        var auth0Id = "auth0|multi-tenant-specific";
        var user = CreateUser(auth0Id);
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();
        
        _dbContext.UserScopeAssignments.Add(new UserScopeAssignment { UserId = user.Id, ScopeType = ScopeType.Tenant, TargetId = tenant1Id, Role = AppRoles.Collector });
        _dbContext.UserScopeAssignments.Add(new UserScopeAssignment { UserId = user.Id, ScopeType = ScopeType.Tenant, TargetId = tenant2Id, Role = AppRoles.OrganisationAdmin });
        _dbContext.SaveChanges();

        _dbContext.RolePermissions.Add(new RolePermission { RoleName = AppRoles.Collector, PermissionKey = "collector.perm" });
        _dbContext.RolePermissions.Add(new RolePermission { RoleName = AppRoles.OrganisationAdmin, PermissionKey = "admin.perm" });
        _dbContext.SaveChanges();

        _currentUserServiceMock.Setup(u => u.UserId).Returns(auth0Id);
        _tenantContextMock.Setup(t => t.TenantId).Returns(tenant2Id); 

        var context = new DefaultHttpContext { RequestServices = _serviceProvider };
        var request = context.Request;

        // Act
        var result = await _functions.GetMe(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<UserDetailDto>>(okResult.Value);
        Assert.Equal(tenant2Id, apiResponse.Data.ActiveTenantId);
        Assert.Contains(AppRoles.OrganisationAdmin, apiResponse.Data.EffectiveRoleKeys);
        Assert.DoesNotContain(AppRoles.Collector, apiResponse.Data.EffectiveRoleKeys);
        Assert.Contains("admin.perm", apiResponse.Data.Permissions);
        Assert.DoesNotContain("collector.perm", apiResponse.Data.Permissions);
    }

    [Fact]
    public async Task GetMe_WithMultipleTenantAssignments_ShouldNotBootstrap()
    {
        // Arrange
        var auth0Id = "auth0|multi-tenant-bootstrap";
        var user = CreateUser(auth0Id);
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();
        
        _dbContext.UserScopeAssignments.Add(new UserScopeAssignment { UserId = user.Id, ScopeType = ScopeType.Tenant, TargetId = tenant1Id, Role = "Admin" });
        _dbContext.UserScopeAssignments.Add(new UserScopeAssignment { UserId = user.Id, ScopeType = ScopeType.Tenant, TargetId = tenant2Id, Role = "Admin" });
        _dbContext.SaveChanges();

        _currentUserServiceMock.Setup(u => u.UserId).Returns(auth0Id);
        _tenantContextMock.Setup(t => t.TenantId).Returns((Guid?)null); 

        var context = new DefaultHttpContext { RequestServices = _serviceProvider };
        var request = context.Request;

        // Act
        var result = await _functions.GetMe(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<UserDetailDto>>(okResult.Value);
        Assert.Empty(apiResponse.Data.EffectiveRoles);
        Assert.Empty(apiResponse.Data.Permissions);
        Assert.Null(apiResponse.Data.ActiveTenantId);
        Assert.Equal(2, apiResponse.Data.ScopeAssignments.Count());
    }
}
