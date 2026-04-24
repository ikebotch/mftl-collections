using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Events.Commands.CreateEvent;
using MFTL.Collections.Application.Features.Public.Commands.InitiatePublicContribution;
using MFTL.Collections.Application.Features.Public.Queries.GetEventBySlug;
using MFTL.Collections.Application.Features.Public.Queries.ListPublicRecipientFunds;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Infrastructure.Payments;
using MFTL.Collections.Infrastructure.Services;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Features.RecipientFunds.Commands.CreateRecipientFund;

namespace MFTL.Collections.Application.Tests.Features.Public;

public class PublicContributionTests
{
    [Fact]
    public async Task CreateEvent_GeneratesSlugAutomatically()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();
        await using var dbContext = CreateDbContext(databaseName, tenantId);

        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var result = await new CreateEventCommandHandler(dbContext, tenantContext).Handle(
            new CreateEventCommand("My Awesome Event", "Description", DateTimeOffset.UtcNow),
            CancellationToken.None);

        result.Slug.Should().Be("my-awesome-event");
    }

    [Fact]
    public async Task GetEventBySlug_PublicLookup_ExcludesPrivateFields()
    {
        // This is more about checking the PublicEventDto structure which we already defined.
        // We can verify that the returned object is indeed a PublicEventDto.
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();
        await using var dbContext = CreateDbContext(databaseName, tenantId);

        var tenantContext = new TestTenantContext { TenantId = tenantId };
        await new CreateEventCommandHandler(dbContext, tenantContext).Handle(
            new CreateEventCommand("Public Event", "Description", DateTimeOffset.UtcNow, "public-slug"),
            CancellationToken.None);

        var queryHandler = new GetEventBySlugQueryHandler(dbContext, tenantContext);
        var result = await queryHandler.Handle(new GetEventBySlugQuery("public-slug"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Slug.Should().Be("public-slug");
        result.Title.Should().Be("Public Event");
        // Verify that Admin-only fields (like IsActive or Metadata) are not in PublicEventDto
        // (This is a compile-time check mostly, but ensures the handler uses the correct DTO)
    }

    [Fact]
    public async Task InitiatePublicContribution_CreatesPendingEntities()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();
        await using var dbContext = CreateDbContext(databaseName, null); // Public context

        // Seed event and fund (using a tenant context)
        var seedTenantContext = new TestTenantContext { TenantId = tenantId };
        await using var seedContext = new CollectionsDbContext(
            new DbContextOptionsBuilder<CollectionsDbContext>().UseInMemoryDatabase(databaseName).Options, 
            seedTenantContext);
        
        var eventDto = await new CreateEventCommandHandler(seedContext, seedTenantContext).Handle(
            new CreateEventCommand("Public Event", "Desc", null, "event-slug"), CancellationToken.None);
        var fundId = await new CreateRecipientFundCommandHandler(seedContext).Handle(
            new CreateRecipientFundCommand(eventDto.Id, "Fund", "Desc", 1000m, null), CancellationToken.None);

        // Initiate public contribution
        var tenantContext = new TestTenantContext();
        var orchestrator = new PaymentOrchestrator();
        var handler = new InitiatePublicContributionCommandHandler(seedContext, tenantContext, orchestrator);

        var result = await handler.Handle(new InitiatePublicContributionCommand(
            "event-slug", fundId, 100m, "GHS", "John Doe", "john@example.com", null, false, "Card", "Note"), 
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PaymentId.Should().NotBeNull();

        // Verify entities in DB
        var contribution = await seedContext.Contributions
            .IgnoreQueryFilters()
            .Include(c => c.Payment)
            .FirstOrDefaultAsync(c => c.EventId == eventDto.Id);

        contribution.Should().NotBeNull();
        contribution!.Status.Should().Be(ContributionStatus.AwaitingPayment);
        contribution.Payment.Should().NotBeNull();
        contribution.Payment!.Status.Should().Be(PaymentStatus.Initiated);
        contribution.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task WebhookSuccess_CreatesReceiptAndIdempotent()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();
        var tenantContext = new TestTenantContext { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<CollectionsDbContext>().UseInMemoryDatabase(databaseName).Options;
        await using var dbContext = new CollectionsDbContext(options, tenantContext);

        // Setup
        var eventDto = await new CreateEventCommandHandler(dbContext, tenantContext).Handle(
            new CreateEventCommand("Event", "Desc", null, "slug"), CancellationToken.None);
        var fundId = await new CreateRecipientFundCommandHandler(dbContext).Handle(
            new CreateRecipientFundCommand(eventDto.Id, "Fund", "Desc", 1000m, null), CancellationToken.None);
        
        var orchestrator = new PaymentOrchestrator();
        var initHandler = new InitiatePublicContributionCommandHandler(dbContext, tenantContext, orchestrator);
        var initResult = await initHandler.Handle(new InitiatePublicContributionCommand(
            "slug", fundId, 100m, "GHS", "John Doe", null, null, false, "Card", null), CancellationToken.None);

        var paymentId = initResult.PaymentId!.Value;
        var payment = await dbContext.Payments.IgnoreQueryFilters().FirstAsync(p => p.Id == paymentId);

        // Webhook
        var settlementService = new ContributionSettlementService(dbContext, new TestCurrentUserService(), new StaticReceiptNumberGenerator("RCT-123"), NullLogger<ContributionSettlementService>.Instance);
        var provider = new MockPaymentProvider();
        var processor = new PaymentWebhookProcessor(dbContext, tenantContext, settlementService, new[] { provider }, NullLogger<PaymentWebhookProcessor>.Instance);

        var payload = "{\"contributionId\":\"" + payment.ContributionId + "\", \"providerReference\":\"" + payment.ProviderReference + "\", \"status\":\"success\"}";
        
        // 1st processing
        await processor.ProcessAsync("Mock", "evt_1", payload, CancellationToken.None);

        var contribution = await dbContext.Contributions.IgnoreQueryFilters().Include(c => c.Receipt).FirstAsync(c => c.Id == payment.ContributionId);
        contribution.Status.Should().Be(ContributionStatus.Completed);
        contribution.Receipt.Should().NotBeNull();

        // 2nd processing (Idempotency)
        var receiptIdBefore = contribution.Receipt!.Id;
        await processor.ProcessAsync("Mock", "evt_1", payload, CancellationToken.None);
        
        var contributionAfter = await dbContext.Contributions.IgnoreQueryFilters().Include(c => c.Receipt).FirstAsync(c => c.Id == payment.ContributionId);
        contributionAfter.Receipt!.Id.Should().Be(receiptIdBefore);
    }

    private static CollectionsDbContext CreateDbContext(string databaseName, Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var tenantContext = new TestTenantContext
        {
            TenantId = tenantId
        };

        return new CollectionsDbContext(options, tenantContext);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? TenantIdentifier { get; set; }
        public bool IsPlatformContext { get; set; }
        public void UseTenant(Guid tenantId, string? identifier = null) { TenantId = tenantId; TenantIdentifier = identifier; }
        public void UsePlatformContext() { IsPlatformContext = true; }
        public void Clear() { TenantId = null; TenantIdentifier = null; IsPlatformContext = false; }
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public string? UserId => null;
        public string? Email => null;
        public System.Security.Claims.ClaimsPrincipal? User => null;
        public bool IsAuthenticated => false;
    }

    private sealed class StaticReceiptNumberGenerator(string receiptNumber) : IReceiptNumberGenerator
    {
        public string Generate() => receiptNumber;
    }
}
