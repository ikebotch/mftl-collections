using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Identity;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MFTL.Collections.Infrastructure.Tests.Identity;

public class ScopedAccessTests
{
    private DbContextOptions<CollectionsDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: "ScopedAccess_" + Guid.NewGuid())
            .Options;
    }

    private Mock<ITenantContext> CreateTenantContextMock(Guid? tenantId = null)
    {
        var mock = new Mock<ITenantContext>();
        mock.Setup(t => t.TenantId).Returns(tenantId);
        return mock;
    }

    [Fact]
    public async Task HasAccessToTenantAsync_ShouldReturnTrue_WhenUserHasPlatformScope()
    {
        // Arrange
        var options = CreateOptions();
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns("user_123");
        var tenantContextMock = CreateTenantContextMock();
        
        using (var context = new CollectionsDbContext(options, tenantContextMock.Object))
        {
            var user = new User { Auth0Id = "user_123", Email = "test@test.com" };
            context.Users.Add(user);
            context.UserScopeAssignments.Add(new UserScopeAssignment 
            { 
                UserId = user.Id, 
                ScopeType = ScopeType.Platform 
            });
            await context.SaveChangesAsync();

            var service = new ScopeAccessService(context, currentUserServiceMock.Object);

            // Act
            var result = await service.HasAccessToTenantAsync(Guid.NewGuid());

            // Assert
            Assert.True(result);
        }
    }

    [Fact]
    public async Task HasAccessToTenantAsync_ShouldReturnFalse_WhenUserHasDifferentTenantScope()
    {
        // Arrange
        var options = CreateOptions();
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns("user_123");
        var tenantContextMock = CreateTenantContextMock();
        
        using (var context = new CollectionsDbContext(options, tenantContextMock.Object))
        {
            var user = new User { Auth0Id = "user_123", Email = "test@test.com" };
            context.Users.Add(user);
            context.UserScopeAssignments.Add(new UserScopeAssignment 
            { 
                UserId = user.Id, 
                ScopeType = ScopeType.Tenant,
                TargetId = Guid.NewGuid() // Different tenant
            });
            await context.SaveChangesAsync();

            var service = new ScopeAccessService(context, currentUserServiceMock.Object);

            // Act
            var result = await service.HasAccessToTenantAsync(Guid.NewGuid());

            // Assert
            Assert.False(result);
        }
    }

    [Fact]
    public async Task HasAccessToRecipientFundAsync_ShouldReturnTrue_WhenUserHasDirectRecipientFundScope()
    {
        // Arrange
        var options = CreateOptions();
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns("user_123");
        
        var fundId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenantContextMock = CreateTenantContextMock(tenantId);

        using (var context = new CollectionsDbContext(options, tenantContextMock.Object))
        {
            var tenant = new Tenant { Id = tenantId, Name = "Tenant 1", Identifier = "t1" };
            var evt = new Event { Id = eventId, TenantId = tenantId, Title = "Event 1" };
            var fund = new RecipientFund { Id = fundId, EventId = eventId, Name = "Fund 1" };
            
            context.Tenants.Add(tenant);
            context.Events.Add(evt);
            context.RecipientFunds.Add(fund);

            var user = new User { Auth0Id = "user_123", Email = "test@test.com" };
            context.Users.Add(user);
            context.UserScopeAssignments.Add(new UserScopeAssignment 
            { 
                UserId = user.Id, 
                ScopeType = ScopeType.RecipientFund,
                TargetId = fundId
            });
            await context.SaveChangesAsync();

            var service = new ScopeAccessService(context, currentUserServiceMock.Object);

            // Act
            var result = await service.HasAccessToRecipientFundAsync(fundId);

            // Assert
            Assert.True(result);
        }
    }

    [Fact]
    public async Task HasAccessToRecipientFundAsync_ShouldReturnFalse_WhenUserHasDifferentRecipientFundScope()
    {
        // Arrange
        var options = CreateOptions();
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns("user_123");
        
        var fundId1 = Guid.NewGuid();
        var fundId2 = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var tenantContextMock = CreateTenantContextMock(tenantId);

        using (var context = new CollectionsDbContext(options, tenantContextMock.Object))
        {
            var tenant = new Tenant { Id = tenantId, Name = "Tenant 1", Identifier = "t1" };
            var evt = new Event { Id = eventId, TenantId = tenantId, Title = "Event 1" };
            var fund1 = new RecipientFund { Id = fundId1, EventId = eventId, Name = "Fund 1" };
            var fund2 = new RecipientFund { Id = fundId2, EventId = eventId, Name = "Fund 2" };
            
            context.Tenants.Add(tenant);
            context.Events.Add(evt);
            context.RecipientFunds.Add(fund1);
            context.RecipientFunds.Add(fund2);

            var user = new User { Auth0Id = "user_123", Email = "test@test.com" };
            context.Users.Add(user);
            context.UserScopeAssignments.Add(new UserScopeAssignment 
            { 
                UserId = user.Id, 
                ScopeType = ScopeType.RecipientFund,
                TargetId = fundId1
            });
            await context.SaveChangesAsync();

            var service = new ScopeAccessService(context, currentUserServiceMock.Object);

            // Act
            var result = await service.HasAccessToRecipientFundAsync(fundId2);

            // Assert
            Assert.False(result);
        }
    }
}
