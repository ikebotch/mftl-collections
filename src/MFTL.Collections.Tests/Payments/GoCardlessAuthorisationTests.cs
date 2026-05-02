using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Payments.Commands.InitiateContributionPayment;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Infrastructure.Services;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace MFTL.Collections.Tests.Payments;

public class GoCardlessAuthorisationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly CollectionsDbContext _dbContext;
    private readonly Mock<IPaymentOrchestrator> _orchestratorMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IScopeAccessService> _scopeServiceMock = new();
    private readonly Mock<INotificationTemplateResolver> _templateResolverMock = new();
    private readonly Mock<ITemplateRenderer> _templateRendererMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<ISmsService> _smsServiceMock = new();
    private readonly TestTenantContext _tenantContext = new();
    private readonly Mock<ILogger<OutboxProcessor>> _outboxLoggerMock = new();

    private class TestTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public Guid? BranchId { get; set; }
        public string? TenantIdentifier { get; set; }
        public IEnumerable<Guid> AllowedTenantIds { get; set; } = new List<Guid>();
        public IEnumerable<Guid> AllowedBranchIds { get; set; } = new List<Guid>();
        public bool IsPlatformContext { get; set; }
        public bool IsSystemContext { get; set; } = true;
        public void SetSystemContext() { IsSystemContext = true; }
        public void ClearContext() { IsSystemContext = false; TenantId = null; }
    }

    public GoCardlessAuthorisationTests(ITestOutputHelper output)
    {
        _output = output;
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new CollectionsDbContext(options, _tenantContext, _currentUserServiceMock.Object);

        _scopeServiceMock.Setup(x => x.CanAccessAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
            
        _templateRendererMock.Setup(x => x.Render(It.IsAny<string>(), It.IsAny<object?>()))
            .Returns((string raw, object? vars) => new RenderedTemplate(raw));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Should_Queue_Authorisation_Event_When_Initiated_With_CheckoutUrl_And_Email()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        _tenantContext.IsSystemContext = true;
        _tenantContext.IsPlatformContext = true;
        _tenantContext.TenantId = tenantId;

        var contributor = new Contributor { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = branchId, Email = "ikebotch@gmail.com", Name = "Ike" };
        var ev = new Event { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = branchId, Title = "Test Event" };
        var fund = new RecipientFund { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = branchId, EventId = ev.Id, Name = "Test Fund" };
        
        var contribution = new Contribution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = branchId,
            EventId = ev.Id,
            RecipientFundId = fund.Id,
            ContributorId = contributor.Id,
            Contributor = contributor,
            Amount = 100m,
            Currency = "GHS",
            Status = ContributionStatus.Pending,
            ContributorName = "Ike",
            Method = "gocardless"
        };
        _dbContext.Contributors.Add(contributor);
        _dbContext.Events.Add(ev);
        _dbContext.RecipientFunds.Add(fund);
        _dbContext.Contributions.Add(contribution);
        await _dbContext.SaveChangesAsync();

        _orchestratorMock.Setup(x => x.InitiatePaymentAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MFTL.Collections.Contracts.Responses.PaymentResult(true, "https://pay.gocardless.com/BR123", "GOCARDLESS-123", Guid.NewGuid(), "Initiated"));

        var handler = new InitiateContributionPaymentCommandHandler(_dbContext, _orchestratorMock.Object, _currentUserServiceMock.Object, _scopeServiceMock.Object);

        // Act
        var result = await handler.Handle(new InitiateContributionPaymentCommand(contribution.Id, "gocardless"), CancellationToken.None);

        // Assert
        Assert.True(result.Success, $"Handler failed: {result.Error}");
        Assert.Equal(ContributionStatus.AwaitingPayment, contribution.Status);
        
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.ContributionId == contribution.Id);
        Assert.NotNull(payment);
        Assert.Equal("https://pay.gocardless.com/BR123", payment.CheckoutUrl);

        var outbox = await _dbContext.OutboxMessages.FirstOrDefaultAsync(m => m.AggregateId == payment.Id && m.EventType == "PaymentAuthorisationRequestedEvent");
        Assert.NotNull(outbox);
        var payload = JsonSerializer.Deserialize<JsonElement>(outbox.Payload);
        Assert.Equal(payment.Id, payload.GetProperty("PaymentId").GetGuid());
        Assert.Equal("https://pay.gocardless.com/BR123", payload.GetProperty("CheckoutUrl").GetString());
    }

    [Fact]
    public async Task Should_Not_Queue_Authorisation_Event_If_No_Email()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        _tenantContext.IsSystemContext = true;
        _tenantContext.IsPlatformContext = true;
        _tenantContext.TenantId = tenantId;

        var contributor = new Contributor { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = branchId, Email = "", Name = "Ike" };
        var ev = new Event { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = branchId, Title = "Test Event" };
        var fund = new RecipientFund { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = branchId, EventId = ev.Id, Name = "Test Fund" };

        var contribution = new Contribution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = branchId,
            EventId = ev.Id,
            RecipientFundId = fund.Id,
            ContributorId = contributor.Id,
            Contributor = contributor,
            Amount = 100m,
            Currency = "GHS",
            Status = ContributionStatus.Pending,
            ContributorName = "Ike",
            Method = "gocardless"
        };
        _dbContext.Contributors.Add(contributor);
        _dbContext.Events.Add(ev);
        _dbContext.RecipientFunds.Add(fund);
        _dbContext.Contributions.Add(contribution);
        await _dbContext.SaveChangesAsync();

        _orchestratorMock.Setup(x => x.InitiatePaymentAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MFTL.Collections.Contracts.Responses.PaymentResult(true, "https://pay.gocardless.com/BR123", "GOCARDLESS-123", Guid.NewGuid(), "Initiated"));

        var handler = new InitiateContributionPaymentCommandHandler(_dbContext, _orchestratorMock.Object, _currentUserServiceMock.Object, _scopeServiceMock.Object);

        // Act
        await handler.Handle(new InitiateContributionPaymentCommand(contribution.Id, "gocardless"), CancellationToken.None);

        // Assert
        var outboxCount = await _dbContext.OutboxMessages.CountAsync(m => m.EventType == "PaymentAuthorisationRequestedEvent");
        Assert.Equal(0, outboxCount);
    }

    [Fact]
    public async Task Should_Be_Idempotent_And_Not_Duplicate_Authorisation_Event()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        _tenantContext.IsSystemContext = true;
        _tenantContext.IsPlatformContext = true;
        _tenantContext.TenantId = tenantId;

        var contributor = new Contributor { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = branchId, Email = "ikebotch@gmail.com", Name = "Ike" };
        var ev = new Event { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = branchId, Title = "Test Event" };
        var fund = new RecipientFund { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = branchId, EventId = ev.Id, Name = "Test Fund" };

        var contribution = new Contribution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = branchId,
            EventId = ev.Id,
            RecipientFundId = fund.Id,
            ContributorId = contributor.Id,
            Contributor = contributor,
            Amount = 100m,
            Currency = "GHS",
            Status = ContributionStatus.Pending,
            ContributorName = "Ike",
            Method = "gocardless"
        };
        _dbContext.Contributors.Add(contributor);
        _dbContext.Events.Add(ev);
        _dbContext.RecipientFunds.Add(fund);
        _dbContext.Contributions.Add(contribution);
        await _dbContext.SaveChangesAsync();

        _orchestratorMock.Setup(x => x.InitiatePaymentAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MFTL.Collections.Contracts.Responses.PaymentResult(true, "https://pay.gocardless.com/BR123", "GOCARDLESS-123", Guid.NewGuid(), "Initiated"));

        var handler = new InitiateContributionPaymentCommandHandler(_dbContext, _orchestratorMock.Object, _currentUserServiceMock.Object, _scopeServiceMock.Object);

        // Act
        await handler.Handle(new InitiateContributionPaymentCommand(contribution.Id, "gocardless"), CancellationToken.None);
        await handler.Handle(new InitiateContributionPaymentCommand(contribution.Id, "gocardless"), CancellationToken.None);

        // Assert
        var outboxCount = await _dbContext.OutboxMessages.CountAsync(m => m.EventType == "PaymentAuthorisationRequestedEvent");
        Assert.Equal(1, outboxCount);
    }

    [Fact]
    public async Task Outbox_Processor_Should_Send_Fallback_Email_When_Template_Missing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        _tenantContext.IsSystemContext = true;
        _tenantContext.TenantId = tenantId;

        var paymentId = Guid.NewGuid();
        var contributionId = Guid.NewGuid();
        var checkoutUrl = "https://pay.gocardless.com/BR123";

        var outbox = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = branchId,
            AggregateId = paymentId,
            AggregateType = "Payment",
            EventType = "PaymentAuthorisationRequestedEvent",
            Payload = JsonSerializer.Serialize(new { PaymentId = paymentId, ContributionId = contributionId, CheckoutUrl = checkoutUrl }),
            Status = OutboxMessageStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString()
        };
        _dbContext.OutboxMessages.Add(outbox);

        var contributor = new Contributor { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = branchId, Email = "ikebotch@gmail.com", Name = "Ike" };
        var ev = new Event { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = branchId, Title = "Test Event" };
        var fund = new RecipientFund { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = branchId, EventId = ev.Id, Name = "Test Fund" };

        var contribution = new Contribution
        {
            Id = contributionId,
            TenantId = tenantId,
            BranchId = branchId,
            EventId = ev.Id,
            RecipientFundId = fund.Id,
            ContributorId = contributor.Id,
            Contributor = contributor,
            Amount = 100m,
            Currency = "GHS",
            Status = ContributionStatus.AwaitingPayment,
            ContributorName = "Ike",
            Method = "gocardless"
        };
        var payment = new Payment { Id = paymentId, TenantId = tenantId, ContributionId = contributionId, Amount = 100m, CheckoutUrl = checkoutUrl, ProviderReference = "GOCARDLESS-123" };
        
        _dbContext.Contributors.Add(contributor);
        _dbContext.Events.Add(ev);
        _dbContext.RecipientFunds.Add(fund);
        _dbContext.Contributions.Add(contribution);
        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync();

        _templateResolverMock.Setup(x => x.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationTemplate)null!); // Template missing

        _emailServiceMock.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(true);

        var processor = new OutboxProcessor(_dbContext, _templateResolverMock.Object, _templateRendererMock.Object, _emailServiceMock.Object, _smsServiceMock.Object, _tenantContext, _outboxLoggerMock.Object);

        // Act
        await processor.ProcessMessagesAsync(10, CancellationToken.None);

        // Assert
        _emailServiceMock.Verify(x => x.SendAsync(
            "ikebotch@gmail.com",
            "Ike",
            "Complete your bank payment",
            It.Is<string>(body => body.Contains(checkoutUrl)),
            null,
            true), Times.Once);

        var processedOutbox = await _dbContext.OutboxMessages.FindAsync(outbox.Id);
        Assert.Equal(OutboxMessageStatus.Sent, processedOutbox!.Status);


        var notification = await _dbContext.Notifications.IgnoreQueryFilters().FirstOrDefaultAsync(n => n.OutboxMessageId == outbox.Id && n.Channel == NotificationChannel.Email);
        Assert.NotNull(notification);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
    }
}
