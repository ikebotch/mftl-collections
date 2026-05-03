using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Events.Queries.GetEventBySlug;
using MFTL.Collections.Application.Features.RecipientFunds.Queries.ListRecipientFundsByEvent;
using MFTL.Collections.Application.Features.Storefront.Commands.CreateStorefrontContribution;
using MFTL.Collections.Application.Features.Storefront.Queries.GetContributionStatus;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MFTL.Collections.Infrastructure.Tests.Storefront;

public class StorefrontSecurityTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CollectionsDbContext _dbContext;

    public StorefrontSecurityTests()
    {
        var services = new ServiceCollection();
        
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<CollectionsDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        var tenantContext = new TestTenantContext();
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns("test-user");

        services.AddSingleton<ITenantContext>(tenantContext);
        services.AddSingleton<ICurrentUserService>(currentUserServiceMock.Object);
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<CollectionsDbContext>());
        
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<CollectionsDbContext>();
    }

    [Fact]
    public async Task GetEventBySlug_OnlyReturnsActiveEvents()
    {
        var tenantId = Guid.NewGuid();
        var activeEvent = new Event { Id = Guid.NewGuid(), Title = "Active", Slug = "active", IsActive = true, TenantId = tenantId };
        var inactiveEvent = new Event { Id = Guid.NewGuid(), Title = "Inactive", Slug = "inactive", IsActive = false, TenantId = tenantId };
        
        _dbContext.Events.AddRange(activeEvent, inactiveEvent);
        await _dbContext.SaveChangesAsync();

        var handler = new GetEventBySlugQueryHandler(_dbContext);

        // 1. Can fetch active
        var result = await handler.Handle(new GetEventBySlugQuery("active"), CancellationToken.None);
        result.Should().NotBeNull();
        result.Title.Should().Be("Active");

        // 2. Cannot fetch inactive
        var act = async () => await handler.Handle(new GetEventBySlugQuery("inactive"), CancellationToken.None);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ListFunds_OnlyReturnsFundsFromActiveEvents()
    {
        var tenantId = Guid.NewGuid();
        var activeEvent = new Event { Id = Guid.NewGuid(), Title = "Active", Slug = "active", IsActive = true, TenantId = tenantId };
        var inactiveEvent = new Event { Id = Guid.NewGuid(), Title = "Inactive", Slug = "inactive", IsActive = false, TenantId = tenantId };
        
        var fund1 = new RecipientFund { Id = Guid.NewGuid(), EventId = activeEvent.Id, Name = "Fund 1", IsActive = true, TenantId = tenantId };
        var fund2 = new RecipientFund { Id = Guid.NewGuid(), EventId = inactiveEvent.Id, Name = "Fund 2", IsActive = true, TenantId = tenantId };
        
        _dbContext.Events.AddRange(activeEvent, inactiveEvent);
        _dbContext.RecipientFunds.AddRange(fund1, fund2);
        await _dbContext.SaveChangesAsync();

        var handler = new ListRecipientFundsByEventQueryHandler(_dbContext);

        // 1. Funds from active event are returned
        var result = await handler.Handle(new ListRecipientFundsByEventQuery(activeEvent.Id), CancellationToken.None);
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Fund 1");

        // 2. Funds from inactive event are NOT returned
        var result2 = await handler.Handle(new ListRecipientFundsByEventQuery(inactiveEvent.Id), CancellationToken.None);
        result2.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateStorefrontContribution_RejectsCash()
    {
        var handler = new CreateStorefrontContributionCommandHandler(_dbContext, _serviceProvider.GetRequiredService<ITenantContext>(), null!);
        
        var command = new CreateStorefrontContributionCommand(
            "any", Guid.NewGuid(), 10, "GHS", "Donor", "0241234567", "mtn", false, "cash", null, null);

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cash payments are not available on the public storefront.");
    }

    [Fact]
    public async Task CreateStorefrontContribution_MoMo_RequiresPhoneAndNetwork()
    {
        var handler = new CreateStorefrontContributionCommandHandler(_dbContext, _serviceProvider.GetRequiredService<ITenantContext>(), null!);
        
        // 1. Missing phone
        var command1 = new CreateStorefrontContributionCommand(
            "any", Guid.NewGuid(), 10, "GHS", "Donor", "0241234567", "mtn", false, "momo", "mtn", null);
        
        var act1 = async () => await handler.Handle(command1 with { DonorPhone = null }, CancellationToken.None);
        await act1.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Donor phone number is required for Mobile Money payments.");

        // 2. Missing network
        var act2 = async () => await handler.Handle(command1 with { DonorNetwork = null }, CancellationToken.None);
        await act2.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Donor network is required for Mobile Money payments.");
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public Guid? BranchId { get; set; }
        public string? TenantIdentifier { get; set; }
        public bool IsPlatformContext { get; set; }
        public bool IsSystemContext { get; set; }
        public IEnumerable<Guid> AllowedTenantIds => TenantId.HasValue ? new[] { TenantId.Value } : [];
        public IEnumerable<Guid> AllowedBranchIds => BranchId.HasValue ? new[] { BranchId.Value } : [];
        public void SetSystemContext() => IsSystemContext = true;
    }
}
