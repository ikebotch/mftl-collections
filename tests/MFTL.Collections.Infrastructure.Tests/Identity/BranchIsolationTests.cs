using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Identity;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MFTL.Collections.Infrastructure.Tests.Identity;

public class BranchIsolationTests
{
    private DbContextOptions<CollectionsDbContext> CreateNewContextOptions()
    {
        return new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: "BranchIsolation_" + Guid.NewGuid())
            .Options;
    }

    [Fact]
    public async Task HasAccessToEventAsync_ShouldReturnTrue_WhenUserAssignedToBranch()
    {
        // Arrange
        var options = CreateNewContextOptions();
        var userId = "user_branch_1";
        var branchId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns(userId);

        var tenantContextMock = new Mock<ITenantContext>();
        var branchContextMock = new Mock<IBranchContext>();

        using (var context = new CollectionsDbContext(options, tenantContextMock.Object, branchContextMock.Object))
        {
            var user = new User { Auth0Id = userId, Email = "branch@test.com" };
            context.Users.Add(user);
            context.UserScopeAssignments.Add(new UserScopeAssignment 
            { 
                UserId = user.Id, 
                ScopeType = ScopeType.Branch, 
                TargetId = branchId 
            });
            var branch = new Branch { Id = branchId, TenantId = tenantId, Name = "Test Branch", Identifier = "test" };
            context.Branches.Add(branch);

            context.Events.Add(new Event 
            { 
                Id = eventId, 
                BranchId = branchId, 
                Title = "Branch Event",
                Slug = "branch-event"
            });
            await context.SaveChangesAsync();

            var service = new ScopeAccessService(context, currentUserServiceMock.Object);

            // Act
            var result = await service.HasAccessToEventAsync(eventId);

            // Assert
            Assert.True(result);
        }
    }

    [Fact]
    public async Task HasAccessToEventAsync_ShouldReturnFalse_WhenUserAssignedToDifferentBranch()
    {
        // Arrange
        var options = CreateNewContextOptions();
        var userId = "user_branch_1";
        var myBranchId = Guid.NewGuid();
        var otherBranchId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns(userId);

        var tenantContextMock = new Mock<ITenantContext>();
        var branchContextMock = new Mock<IBranchContext>();

        using (var context = new CollectionsDbContext(options, tenantContextMock.Object, branchContextMock.Object))
        {
            var user = new User { Auth0Id = userId, Email = "branch@test.com" };
            context.Users.Add(user);
            context.UserScopeAssignments.Add(new UserScopeAssignment 
            { 
                UserId = user.Id, 
                ScopeType = ScopeType.Branch, 
                TargetId = myBranchId 
            });
            var myBranch = new Branch { Id = myBranchId, TenantId = tenantId, Name = "My Branch", Identifier = "my" };
            var otherBranch = new Branch { Id = otherBranchId, TenantId = tenantId, Name = "Other Branch", Identifier = "other" };
            context.Branches.AddRange(myBranch, otherBranch);

            context.Events.Add(new Event 
            { 
                Id = eventId, 
                BranchId = otherBranchId, 
                Title = "Other Branch Event",
                Slug = "other-branch-event"
            });
            await context.SaveChangesAsync();

            var service = new ScopeAccessService(context, currentUserServiceMock.Object);

            // Act
            var result = await service.HasAccessToEventAsync(eventId);

            // Assert
            Assert.False(result);
        }
    }

    [Fact]
    public async Task GetAccessibleEventIdsAsync_ShouldOnlyReturnEventsFromUserBranch()
    {
        // Arrange
        var options = CreateNewContextOptions();
        var userId = "user_branch_1";
        var branch1Id = Guid.NewGuid();
        var branch2Id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns(userId);

        var tenantContextMock = new Mock<ITenantContext>();
        var branchContextMock = new Mock<IBranchContext>();

        using (var context = new CollectionsDbContext(options, tenantContextMock.Object, branchContextMock.Object))
        {
            var user = new User { Auth0Id = userId, Email = "branch@test.com" };
            context.Users.Add(user);
            context.UserScopeAssignments.Add(new UserScopeAssignment 
            { 
                UserId = user.Id, 
                ScopeType = ScopeType.Branch, 
                TargetId = branch1Id 
            });

            var b1 = new Branch { Id = branch1Id, TenantId = tenantId, Name = "B1", Identifier = "b1" };
            var b2 = new Branch { Id = branch2Id, TenantId = tenantId, Name = "B2", Identifier = "b2" };
            var b3Id = Guid.NewGuid();
            var b3 = new Branch { Id = b3Id, TenantId = tenantId, Name = "Global", Identifier = "global" };
            context.Branches.AddRange(b1, b2, b3);

            context.Events.Add(new Event { Id = Guid.NewGuid(), BranchId = branch1Id, Title = "B1 Event 1", Slug = "b1-e1" });
            context.Events.Add(new Event { Id = Guid.NewGuid(), BranchId = branch1Id, Title = "B1 Event 2", Slug = "b1-e2" });
            context.Events.Add(new Event { Id = Guid.NewGuid(), BranchId = branch2Id, Title = "B2 Event", Slug = "b2-e1" });
            context.Events.Add(new Event { Id = Guid.NewGuid(), BranchId = b3Id, Title = "Global Event", Slug = "global-e1" });

            await context.SaveChangesAsync();

            var service = new ScopeAccessService(context, currentUserServiceMock.Object);

            // Act
            var result = await service.GetAccessibleEventIdsAsync(tenantId);

            // Assert
            Assert.Equal(2, result.Count());
        }
    }
}
