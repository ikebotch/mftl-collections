using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Contributions.Commands.RecordCashContribution;
using MFTL.Collections.Application.Features.Collectors.Queries.ListCollectorHistory;
using MFTL.Collections.Application.Features.Receipts.Queries.GetReceiptById;
using MFTL.Collections.Application.Features.Events.Commands.CreateEvent;
using MFTL.Collections.Application.Features.Events.Queries.GetEventById;
using MFTL.Collections.Application.Features.RecipientFunds.Commands.CreateRecipientFund;
using MFTL.Collections.Application.Features.RecipientFunds.Queries.ListRecipientFundsByEvent;
using MFTL.Collections.Domain.Entities;
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
        var branchId = Guid.NewGuid();
        var createdEvent = await new CreateEventCommandHandler(writeContext).Handle(
            new CreateEventCommand("Tenant event", "Live tenant readback", DateTimeOffset.UtcNow, branchId),
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
        var branchId = Guid.NewGuid();
        var createdEvent = await new CreateEventCommandHandler(writeContext).Handle(
            new CreateEventCommand("Tenant A event", "Hidden from tenant B", DateTimeOffset.UtcNow, branchId),
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
        var branchId = Guid.NewGuid();
        var createdEvent = await new CreateEventCommandHandler(seedContext).Handle(
            new CreateEventCommand("Fund event", "Recipient fund test", DateTimeOffset.UtcNow, branchId),
            CancellationToken.None);

        await new CreateRecipientFundCommandHandler(seedContext).Handle(
            new CreateRecipientFundCommand(createdEvent.Id, "Education Fund", "Books and fees", 2500m, true, null),
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
        const string collectorAuth0Id = "collector-auth0";

        await using var dbContext = CreateDbContext(databaseName, tenantId);
        var branchId = Guid.NewGuid();
        var createdEvent = await new CreateEventCommandHandler(dbContext).Handle(
            new CreateEventCommand("Collector event", "Cash collection", DateTimeOffset.UtcNow, branchId),
            CancellationToken.None);

        var fundId = await new CreateRecipientFundCommandHandler(dbContext).Handle(
            new CreateRecipientFundCommand(createdEvent.Id, "Medical Fund", "Emergency support", 5000m, true, null),
            CancellationToken.None);

        var collector = await CreateCollectorAsync(dbContext, createdEvent.Id, fundId, collectorAuth0Id);

        var settlementService = new ContributionSettlementService(
            dbContext,
            new TestCurrentUserService(collectorAuth0Id),
            new StaticReceiptNumberGenerator("RCT-TEST-0001"),
            NullLogger<ContributionSettlementService>.Instance);

        var result = await new RecordCashContributionCommandHandler(dbContext, new TestCurrentUserService(collectorAuth0Id), settlementService).Handle(
            new RecordCashContributionCommand(
                createdEvent.Id,
                fundId,
                150m,
                "GHS",
                "Collector donor",
                "+233241234567",
                "collector@example.com",
                false,
                "cash",
                "Live verification",
                collectorAuth0Id),
            CancellationToken.None);

        var contribution = await dbContext.Contributions
            .Include(c => c.Branch)
            .Include(c => c.RecipientFund)
            .Include(c => c.Receipt)
            .FirstAsync(c => c.Id == result.ContributionId);

        contribution.Status.Should().Be(ContributionStatus.Completed);
        contribution.RecipientFund.CollectedAmount.Should().Be(150m);
        contribution.Branch.TenantId.Should().Be(tenantId);
        contribution.Receipt.Should().NotBeNull();
        contribution.Receipt!.ReceiptNumber.Should().Be("RCT-TEST-0001");
        contribution.Receipt.RecordedByUserId.Should().Be(collector.Id);
    }

    [Fact]
    public async Task Receipt_CanBeReadBackBySameTenant()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();

        var receiptId = await CreateCashContributionWithReceiptAsync(databaseName, tenantId, "RCT-TEST-0002");

        await using var readContext = CreateDbContext(databaseName, tenantId);
        var receipt = await new GetReceiptByIdQueryHandler(readContext).Handle(new GetReceiptByIdQuery(receiptId), CancellationToken.None);

        receipt.Id.Should().Be(receiptId);
        receipt.ReceiptNumber.Should().Be("RCT-TEST-0002");
        receipt.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task TenantA_CannotReadTenantBReceipt()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();

        var receiptId = await CreateCashContributionWithReceiptAsync(databaseName, tenantA, "RCT-TEST-0003");

        await using var readContext = CreateDbContext(databaseName, tenantB);
        var act = async () => await new GetReceiptByIdQueryHandler(readContext).Handle(new GetReceiptByIdQuery(receiptId), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Receipt not found.");
    }

    [Fact]
    public async Task MissingReceipt_ReturnsClearNotFoundResponse()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();

        await using var context = CreateDbContext(databaseName, tenantId);
        var act = async () => await new GetReceiptByIdQueryHandler(context).Handle(new GetReceiptByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Receipt not found.");
    }

    [Fact]
    public async Task NoAssignment_BlocksCollection()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();
        const string collectorAuth0Id = "collector-no-assignment";

        await using var dbContext = CreateDbContext(databaseName, tenantId);
        var branchId = Guid.NewGuid();
        var createdEvent = await new CreateEventCommandHandler(dbContext).Handle(
            new CreateEventCommand("Collector event", "Cash collection", DateTimeOffset.UtcNow, branchId),
            CancellationToken.None);

        var fundId = await new CreateRecipientFundCommandHandler(dbContext).Handle(
            new CreateRecipientFundCommand(createdEvent.Id, "Medical Fund", "Emergency support", 5000m, true, null),
            CancellationToken.None);

        await CreateCollectorAsync(dbContext, null, null, collectorAuth0Id);

        var settlementService = new ContributionSettlementService(
            dbContext,
            new TestCurrentUserService(collectorAuth0Id),
            new StaticReceiptNumberGenerator("RCT-TEST-0100"),
            NullLogger<ContributionSettlementService>.Instance);

        var act = async () => await new RecordCashContributionCommandHandler(dbContext, new TestCurrentUserService(collectorAuth0Id), settlementService).Handle(
            new RecordCashContributionCommand(
                createdEvent.Id,
                fundId,
                150m,
                "GHS",
                "Collector donor",
                "+233241234567",
                null,
                false,
                "cash",
                "Blocked",
                collectorAuth0Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Collector is not assigned to this event.");
    }

    [Fact]
    public async Task InactiveCollector_IsBlocked()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();
        const string collectorAuth0Id = "collector-inactive";

        await using var dbContext = CreateDbContext(databaseName, tenantId);
        var branchId = Guid.NewGuid();
        var createdEvent = await new CreateEventCommandHandler(dbContext).Handle(
            new CreateEventCommand("Collector event", "Cash collection", DateTimeOffset.UtcNow, branchId),
            CancellationToken.None);

        var fundId = await new CreateRecipientFundCommandHandler(dbContext).Handle(
            new CreateRecipientFundCommand(createdEvent.Id, "Medical Fund", "Emergency support", 5000m, true, null),
            CancellationToken.None);

        await CreateCollectorAsync(dbContext, createdEvent.Id, fundId, collectorAuth0Id, isActive: false);

        var settlementService = new ContributionSettlementService(
            dbContext,
            new TestCurrentUserService(collectorAuth0Id),
            new StaticReceiptNumberGenerator("RCT-TEST-0101"),
            NullLogger<ContributionSettlementService>.Instance);

        var act = async () => await new RecordCashContributionCommandHandler(dbContext, new TestCurrentUserService(collectorAuth0Id), settlementService).Handle(
            new RecordCashContributionCommand(
                createdEvent.Id,
                fundId,
                150m,
                "GHS",
                "Collector donor",
                "+233241234567",
                null,
                false,
                "cash",
                "Blocked",
                collectorAuth0Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Collector is inactive.");
    }

    [Fact]
    public async Task CollectorHistory_OnlyShowsOwnReceipts()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();

        await using var dbContext = CreateDbContext(databaseName, tenantId);
        var branchId = Guid.NewGuid();
        var createdEvent = await new CreateEventCommandHandler(dbContext).Handle(
            new CreateEventCommand("Collector event", "Cash collection", DateTimeOffset.UtcNow, branchId),
            CancellationToken.None);

        var fundId = await new CreateRecipientFundCommandHandler(dbContext).Handle(
            new CreateRecipientFundCommand(createdEvent.Id, "Medical Fund", "Emergency support", 5000m, true, null),
            CancellationToken.None);

        await CreateCollectorAsync(dbContext, createdEvent.Id, fundId, "collector-one");
        await CreateCollectorAsync(dbContext, createdEvent.Id, fundId, "collector-two");

        var firstSettlementService = new ContributionSettlementService(
            dbContext,
            new TestCurrentUserService("collector-one"),
            new StaticReceiptNumberGenerator("RCT-TEST-0201"),
            NullLogger<ContributionSettlementService>.Instance);

        var secondSettlementService = new ContributionSettlementService(
            dbContext,
            new TestCurrentUserService("collector-two"),
            new StaticReceiptNumberGenerator("RCT-TEST-0202"),
            NullLogger<ContributionSettlementService>.Instance);

        var handlerOne = new RecordCashContributionCommandHandler(dbContext, new TestCurrentUserService("collector-one"), firstSettlementService);
        var handlerTwo = new RecordCashContributionCommandHandler(dbContext, new TestCurrentUserService("collector-two"), secondSettlementService);

        await handlerOne.Handle(new RecordCashContributionCommand(
            createdEvent.Id,
            fundId,
            150m,
            "GHS",
            "Collector One Donor",
            "+233241234567",
            null,
            false,
            "cash",
            null,
            "collector-one"), CancellationToken.None);

        await handlerTwo.Handle(new RecordCashContributionCommand(
            createdEvent.Id,
            fundId,
            200m,
            "GHS",
            "Collector Two Donor",
            "+233241234568",
            null,
            false,
            "cash",
            null,
            "collector-two"), CancellationToken.None);

        var history = await new ListCollectorHistoryQueryHandler(dbContext, new TestCurrentUserService("collector-one"))
            .Handle(new ListCollectorHistoryQuery("collector-one"), CancellationToken.None);

        history.Should().ContainSingle();
        history.Single().ReceiptNumber.Should().Be("RCT-TEST-0201");
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

        var branchContext = new TestBranchContext();

        return new CollectionsDbContext(options, tenantContext, branchContext);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TenantId { get; init; }
        public IReadOnlyList<Guid> TenantIds => TenantId.HasValue ? new[] { TenantId.Value } : Array.Empty<Guid>();
        public string? TenantIdentifier { get; init; }
        public bool IsPlatformContext { get; init; }
    }

    private sealed class TestBranchContext : IBranchContext
    {
        public Guid? BranchId { get; set; }
        public IReadOnlyList<Guid> BranchIds => BranchId.HasValue ? new[] { BranchId.Value } : Array.Empty<Guid>();
        public bool IsGlobalContext => !BranchId.HasValue;
        public void UseBranch(Guid branchId) => BranchId = branchId;
        public void UseBranches(IEnumerable<Guid> branchIds) => BranchId = branchIds.FirstOrDefault();
        public void UseGlobalContext() => BranchId = null;
        public void Clear() => BranchId = null;
    }

    private sealed class TestCurrentUserService(string? userId = null, string? email = null, bool isPlatformAdmin = false) : ICurrentUserService
    {
        public string? UserId => userId;
        public string? Email => email;
        public System.Security.Claims.ClaimsPrincipal? User => null;
        public bool IsAuthenticated => !string.IsNullOrEmpty(userId);
        public bool IsPlatformAdmin => isPlatformAdmin;
        public IEnumerable<string> Roles => Enumerable.Empty<string>();
    }

    private sealed class StaticReceiptNumberGenerator(string receiptNumber) : IReceiptNumberGenerator
    {
        public string Generate() => receiptNumber;
    }

    private static async Task<Guid> CreateCashContributionWithReceiptAsync(string databaseName, Guid tenantId, string receiptNumber)
    {
        await using var dbContext = CreateDbContext(databaseName, tenantId);
        const string collectorAuth0Id = "collector-receipt";
        var branchId = Guid.NewGuid();
        var createdEvent = await new CreateEventCommandHandler(dbContext).Handle(
            new CreateEventCommand("Receipt event", "Receipt readback", DateTimeOffset.UtcNow, branchId),
            CancellationToken.None);

        var fundId = await new CreateRecipientFundCommandHandler(dbContext).Handle(
            new CreateRecipientFundCommand(createdEvent.Id, "Support Fund", "Receipt support", 1000m, true, null),
            CancellationToken.None);

        await CreateCollectorAsync(dbContext, createdEvent.Id, fundId, collectorAuth0Id);

        var settlementService = new ContributionSettlementService(
            dbContext,
            new TestCurrentUserService(collectorAuth0Id),
            new StaticReceiptNumberGenerator(receiptNumber),
            NullLogger<ContributionSettlementService>.Instance);

        var result = await new RecordCashContributionCommandHandler(dbContext, new TestCurrentUserService(collectorAuth0Id), settlementService).Handle(
            new RecordCashContributionCommand(
                createdEvent.Id,
                fundId,
                99m,
                "GHS",
                "Receipt donor",
                "+233241234567",
                "receipt@example.com",
                false,
                "cash",
                "Receipt note",
                collectorAuth0Id),
            CancellationToken.None);

        result.ReceiptId.Should().NotBeNull();
        return result.ReceiptId!.Value;
    }

    private static async Task<User> CreateCollectorAsync(
        CollectionsDbContext dbContext,
        Guid? eventId,
        Guid? fundId,
        string auth0Id,
        bool isActive = true)
    {
        var user = new User
        {
            Auth0Id = auth0Id,
            Email = $"{auth0Id}@mftl.local",
            Name = auth0Id,
            IsActive = isActive,
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        if (eventId.HasValue)
        {
            dbContext.UserScopeAssignments.Add(new UserScopeAssignment
            {
                UserId = user.Id,
                ScopeType = ScopeType.Event,
                TargetId = eventId,
                Role = "Collector",
            });
        }

        if (fundId.HasValue)
        {
            dbContext.UserScopeAssignments.Add(new UserScopeAssignment
            {
                UserId = user.Id,
                ScopeType = ScopeType.RecipientFund,
                TargetId = fundId,
                Role = "Collector",
            });
        }

        await dbContext.SaveChangesAsync();
        return user;
    }
}
