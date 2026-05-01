using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Api.Functions.Users;
using MFTL.Collections.Api.Middleware;
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
using Microsoft.Extensions.Options;
using MFTL.Collections.Infrastructure.Configuration;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Tests;

public class TenantFallbackTests : IDisposable
{
    private readonly CollectionsDbContext _dbContext;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly FunctionHttpRequestAccessor _requestAccessor;

    public TenantFallbackTests()
    {
        var services = new ServiceCollection();
        
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        _tenantContextMock = new Mock<ITenantContext>();
        _dbContext = new CollectionsDbContext(options, _tenantContextMock.Object, Mock.Of<ICurrentUserService>());

        _requestAccessor = new FunctionHttpRequestAccessor();
        
        services.AddSingleton<ITenantResolver, HeaderTenantResolver>();
        services.AddSingleton<ITenantResolver, QueryTenantResolver>();
        services.AddSingleton<CompositeTenantResolver>();
        services.AddSingleton(_requestAccessor);
        services.AddSingleton<IOptions<TenantResolutionOptions>>(Options.Create(new TenantResolutionOptions
        {
            HeaderName = "X-Tenant-Id"
        }));
        
        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task Resolver_WithHeader_ShouldTakePrecedence()
    {
        // Arrange
        var headerTenantId = Guid.NewGuid();
        var queryTenantId = Guid.NewGuid();
        
        _requestAccessor.SetRequest(
            new Dictionary<string, string[]> { { "X-Tenant-Id", new[] { headerTenantId.ToString() } } },
            new Dictionary<string, string[]> { { "tenantId", new[] { queryTenantId.ToString() } } },
            "localhost",
            "GET");

        var resolver = _serviceProvider.GetRequiredService<CompositeTenantResolver>();

        // Act
        var result = await resolver.ResolveAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(headerTenantId, result.TenantId);
    }

    [Fact]
    public async Task Resolver_WithOnlyQuery_ShouldFallbackOnGet()
    {
        // Arrange
        var queryTenantId = Guid.NewGuid();
        
        _requestAccessor.SetRequest(
            new Dictionary<string, string[]>(),
            new Dictionary<string, string[]> { { "tenantId", new[] { queryTenantId.ToString() } } },
            "localhost",
            "GET");

        var resolver = _serviceProvider.GetRequiredService<CompositeTenantResolver>();

        // Act
        var result = await resolver.ResolveAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(queryTenantId, result.TenantId);
    }

    [Fact]
    public async Task Resolver_WithOnlyQuery_ShouldNotFallbackOnPost()
    {
        // Arrange
        var queryTenantId = Guid.NewGuid();
        
        _requestAccessor.SetRequest(
            new Dictionary<string, string[]>(),
            new Dictionary<string, string[]> { { "tenantId", new[] { queryTenantId.ToString() } } },
            "localhost",
            "POST");

        var resolver = _serviceProvider.GetRequiredService<CompositeTenantResolver>();

        // Act
        var result = await resolver.ResolveAsync();

        // Assert
        Assert.False(result.Success);
    }
    [Fact]
    public async Task Resolver_WithCsvHeader_ShouldResolveFirstId()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        
        _requestAccessor.SetRequest(
            new Dictionary<string, string[]> { { "X-Tenant-Id", new[] { $"{tenantA},{tenantB}" } } },
            new Dictionary<string, string[]>(),
            "localhost",
            "GET");
            
        var resolver = _serviceProvider.GetRequiredService<CompositeTenantResolver>();
        
        // Act
        var result = await resolver.ResolveAsync();
        
        // Assert
        Assert.True(result.Success);
        Assert.Equal(tenantA, result.TenantId);
    }

    [Fact]
    public async Task ScopeAccessService_ShouldNotUnionPermissionsAcrossTenants()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var auth0Id = "auth0|test-user";
        var permission = "test.permission";
        
        var user = new User { Id = Guid.NewGuid(), Auth0Id = auth0Id };
        _dbContext.Users.Add(user);
        
        // User is Admin in Tenant A, but only Viewer in Tenant B
        _dbContext.UserScopeAssignments.Add(new UserScopeAssignment 
        { 
            UserId = user.Id, 
            ScopeType = ScopeType.Tenant, 
            TargetId = tenantA, 
            Role = AppRoles.OrganisationAdmin 
        });
        _dbContext.UserScopeAssignments.Add(new UserScopeAssignment 
        { 
            UserId = user.Id, 
            ScopeType = ScopeType.Tenant, 
            TargetId = tenantB, 
            Role = AppRoles.Viewer 
        });
        
        // Tenant Admin has the permission, Viewer does not
        _dbContext.RolePermissions.Add(new RolePermission { RoleName = AppRoles.OrganisationAdmin, PermissionKey = permission });
        _dbContext.RolePermissions.Add(new RolePermission { RoleName = AppRoles.Viewer, PermissionKey = "other.permission" });
        
        await _dbContext.SaveChangesAsync();
        
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(x => x.UserId).Returns(auth0Id);
        
        var scopeService = new MFTL.Collections.Infrastructure.Identity.ScopeAccessService(
            _dbContext, 
            currentUserServiceMock.Object, 
            _tenantContextMock.Object,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<MFTL.Collections.Infrastructure.Identity.ScopeAccessService>());
            
        // Act & Assert
        
        // Access to Tenant A should be allowed
        var allowedA = await scopeService.CanAccessAsync(permission, tenantA);
        Assert.True(allowedA);
        
        // Access to Tenant B should be denied (even though user has Admin in A)
        var allowedB = await scopeService.CanAccessAsync(permission, tenantB);
        Assert.False(allowedB);
    }
}
