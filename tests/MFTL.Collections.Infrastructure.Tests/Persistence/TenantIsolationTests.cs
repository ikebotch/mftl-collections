using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Contributions.Commands.RecordCashContribution;
using MFTL.Collections.Application.Features.Events.Commands.CreateEvent;
using MFTL.Collections.Application.Features.Events.Queries.GetEventById;
using MFTL.Collections.Application.Features.RecipientFunds.Commands.CreateRecipientFund;
using MFTL.Collections.Application.Features.RecipientFunds.Queries.ListRecipientFundsByEvent;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Infrastructure.Services;

namespace MFTL.Collections.Infrastructure.Tests.Persistence;

public class TenantIsolationTests
{
    [Fact]
    public async Task CreateEvent_WithTenantContext_CanBeReadBackBySameTenant()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();

        await using var writeContext = CreateDbContext(databaseName, tenantId);
        var createdEvent = await new CreateEventCommandHandler(writeContext).Handle(
            new CreateEventCommand("Tenant event", "Live tenant readback", DateTimeOffset.UtcNow),
            CancellationToken.None);

        await using var readContext = CreateDbContext(databaseName, tenantId);
        var reloadedEvent = await new GetEventByIdQueryHandler(readContext).Handle(
            new GetEventByIdQuery(createdEvent.Id),
            CancellationToken.None);

        reloadedEvent.Id.Should().Be(createdEvent.Id);
        reloadedEvent.Title.Should().Be("Tenant event");
    }

    [Fact]
    public async Task TenantA_CannotReadTenantBEvent()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();

        await using var writeContext = CreateDbContext(databaseName, tenantA);
        var createdEvent = await new CreateEventCommandHandler(writeContext).Handle(
            new CreateEventCommand("Tenant A event", "Hidden from tenant B", DateTimeOffset.UtcNow),
            CancellationToken.None);

        await using var readContext = CreateDbContext(databaseName, tenantB);
        var act = async () => await new GetEventByIdQueryHandler(readContext).Handle(
            new GetEventByIdQuery(createdEvent.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Event not found.");
    }

    [Fact]
    public async Task CreateRecipientFund_WithTenantContext_CanBeReadBackBySameTenant()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();

        await using var seedContext = CreateDbContext(databaseName, tenantId);
        var createdEvent = await new CreateEventCommandHandler(seedContext).Handle(
            new CreateEventCommand("Fund event", "Recipient fund test", DateTimeOffset.UtcNow),
            CancellationToken.None);

        await new CreateRecipientFundCommandHandler(seedContext).Handle(
            new CreateRecipientFundCommand(createdEvent.Id, "Education Fund", "Books and fees", 2500m, null),
            CancellationToken.None);

        await using var readContext = CreateDbContext(databaseName, tenantId);
        var funds = await new ListRecipientFundsByEventQueryHandler(readContext).Handle(
            new ListRecipientFundsByEventQuery(createdEvent.Id),
            CancellationToken.None);

        funds.Should().ContainSingle();
        funds.Single().Name.Should().Be("Education Fund");
    }

    [Fact]
    public async Task CashContributionSettlement_CanReadCreatedContributionWithinSameTenantContext()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();

        await using var dbContext = CreateDbContext(databaseName, tenantId);
        var createdEvent = await new CreateEventCommandHandler(dbContext).Handle(
            new CreateEventCommand("Collector event", "Cash collection", DateTimeOffset.UtcNow),
            CancellationToken.None);

        var fundId = await new CreateRecipientFundCommandHandler(dbContext).Handle(
            new CreateRecipientFundCommand(createdEvent.Id, "Medical Fund", "Emergency support", 5000m, null),
            CancellationToken.None);

        var settlementService = new ContributionSettlementService(
            dbContext,
            NullLogger<ContributionSettlementService>.Instance);

        var contributionId = await new RecordCashContributionCommandHandler(dbContext, settlementService).Handle(
            new RecordCashContributionCommand(createdEvent.Id, fundId, 150m, "Collector donor", "Live verification"),
            CancellationToken.None);

        var contribution = await dbContext.Contributions
            .Include(c => c.RecipientFund)
            .FirstAsync(c => c.Id == contributionId);

        contribution.Status.Should().Be(ContributionStatus.Completed);
        contribution.RecipientFund.CollectedAmount.Should().Be(150m);
        contribution.TenantId.Should().Be(tenantId);
    }

    private static CollectionsDbContext CreateDbContext(string databaseName, Guid? tenantId, bool platformAccess = false)
    {
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var tenantContext = new TestTenantContext
        {
            TenantId = tenantId,
            IsPlatformContext = platformAccess,
        };

        return new CollectionsDbContext(options, tenantContext);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TenantId { get; init; }
        public string? TenantIdentifier { get; init; }
        public bool IsPlatformContext { get; init; }
    }
}
