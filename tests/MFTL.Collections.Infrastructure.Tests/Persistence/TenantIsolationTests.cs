using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using NSubstitute;
using FluentAssertions;

namespace MFTL.Collections.Infrastructure.Tests.Persistence;

public class TenantIsolationTests
{
    [Fact]
    public async Task DbContext_ShouldApplyTenantFilter()
    {
        // Arrange
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: "TenantIsolationDb")
            .Options;

        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(tenant1);

        using (var context = new CollectionsDbContext(options, tenantContext))
        {
            context.Events.Add(new Event { Title = "Event T1", TenantId = tenant1 });
            context.Events.Add(new Event { Title = "Event T2", TenantId = tenant2 });
            await context.SaveChangesAsync();
        }

        // Act
        using (var context = new CollectionsDbContext(options, tenantContext))
        {
            var events = await context.Events.ToListAsync();

            // Assert
            events.Should().HaveCount(1);
            events.Single().Title.Should().Be("Event T1");
        }
    }
}
