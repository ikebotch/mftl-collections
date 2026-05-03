using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Identity;
using MFTL.Collections.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MFTL.Collections.Infrastructure.Tests.Identity;

public class ScopedAccessTests
{
    [Fact]
    public async Task HasAccessToTenantAsync_ShouldReturnTrue_WhenUserHasPlatformScope()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: "ScopedAccess_" + Guid.NewGuid())
            .Options;

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns("user_123");
        
        using (var context = new CollectionsDbContext(options, null!, currentUserServiceMock.Object))
        {
            var user = new MFTL.Collections.Domain.Entities.User { Auth0Id = "user_123", Email = "test@test.com" };
            context.Users.Add(user);
            context.UserScopeAssignments.Add(new MFTL.Collections.Domain.Entities.UserScopeAssignment 
            { 
                UserId = user.Id, 
                ScopeType = MFTL.Collections.Domain.Entities.ScopeType.Platform 
            });
            await context.SaveChangesAsync();

            var service = new ScopeAccessService(context, currentUserServiceMock.Object, new Mock<ITenantContext>().Object, NullLogger<ScopeAccessService>.Instance);

            // Act
            var result = await service.HasAccessToTenantAsync(Guid.NewGuid());

            // Assert
            Assert.True(result);
        }
    }
}
