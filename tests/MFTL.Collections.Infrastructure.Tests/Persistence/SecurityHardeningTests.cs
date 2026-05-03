using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Infrastructure.Persistence;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MFTL.Collections.Infrastructure.Tests.Persistence;

public class SecurityHardeningTests
{
    private static CollectionsDbContext CreateDbContext(string databaseName, Guid? tenantId, Guid? branchId = null, bool platformAccess = false, bool systemAccess = false)
    {
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var tenantContext = new TestTenantContext
        {
            TenantId = tenantId,
            BranchId = branchId,
            IsPlatformContext = platformAccess,
            IsSystemContext = systemAccess
        };

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns("test-user");

        return new CollectionsDbContext(options, tenantContext, currentUserServiceMock.Object);
    }

    [Fact]
    public async Task NormalRequest_CannotSpoofTenantId()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();

        await using var dbContext = CreateDbContext(databaseName, tenantA);
        
        var maliciousEvent = new Event
        {
            Title = "Spoofed Event",
            TenantId = tenantB // Trying to save to Tenant B while in Tenant A context
        };

        dbContext.Events.Add(maliciousEvent);
        
        var act = async () => await dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Cannot spoof TenantId.");
    }

    [Fact]
    public async Task NormalRequest_CannotSpoofBranchId()
    {
        var tenantId = Guid.NewGuid();
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();

        await using var dbContext = CreateDbContext(databaseName, tenantId, branchA);
        
        var maliciousEvent = new Event
        {
            Title = "Spoofed Branch Event",
            BranchId = branchB // Trying to save to Branch B while in Branch A context
        };

        dbContext.Events.Add(maliciousEvent);
        
        var act = async () => await dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Cannot spoof BranchId.");
    }

    [Fact]
    public async Task NormalRequest_RequiresTenantContext()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using var dbContext = CreateDbContext(databaseName, null); // No tenant in context
        
        var newEvent = new Event { Title = "No Context Event" };
        dbContext.Events.Add(newEvent);
        
        var act = async () => await dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tenant context is required for non-system requests.");
    }

    [Fact]
    public async Task SystemContext_CanSaveWithPresetIds()
    {
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();

        // No tenant/branch in context, but IsSystemContext = true
        await using var dbContext = CreateDbContext(databaseName, null, null, systemAccess: true);
        
        var systemEvent = new Event
        {
            Title = "System Event",
            TenantId = tenantId,
            BranchId = branchId
        };

        dbContext.Events.Add(systemEvent);
        await dbContext.SaveChangesAsync();

        // Verify it saved with the preset IDs
        var savedEvent = await dbContext.Events.IgnoreQueryFilters().FirstAsync(e => e.Id == systemEvent.Id);
        savedEvent.TenantId.Should().Be(tenantId);
        savedEvent.BranchId.Should().Be(branchId);
    }

    [Fact]
    public async Task NormalRequest_AutomaticallyFillsTenantId()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();

        await using var dbContext = CreateDbContext(databaseName, tenantId);
        
        var newEvent = new Event { Title = "Auto Tenant Event" };
        dbContext.Events.Add(newEvent);
        
        await dbContext.SaveChangesAsync();

        newEvent.TenantId.Should().Be(tenantId);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TenantId { get; init; }
        public Guid? BranchId { get; init; }
        public string? TenantIdentifier { get; init; }
        public bool IsPlatformContext { get; init; }
        public bool IsSystemContext { get; set; }
        public IEnumerable<Guid> AllowedTenantIds => TenantId.HasValue ? new[] { TenantId.Value } : [];
        public IEnumerable<Guid> AllowedBranchIds => BranchId.HasValue ? new[] { BranchId.Value } : [];
        public void SetSystemContext() => IsSystemContext = true;
    }
}
