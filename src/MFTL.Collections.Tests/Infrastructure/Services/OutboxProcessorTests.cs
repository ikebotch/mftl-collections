using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using System.Text.Json;

namespace MFTL.Collections.Tests.Infrastructure.Services;

public class OutboxProcessorTests
{
    private readonly Mock<ITenantContext> _tenantContextMock = new();
    private readonly Mock<INotificationTemplateResolver> _templateResolverMock = new();
    private readonly Mock<ITemplateRenderer> _templateRendererMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<ISmsService> _smsServiceMock = new();
    private readonly Mock<ILogger<OutboxProcessor>> _loggerMock = new();
    private readonly CollectionsDbContext _dbContext;

    public OutboxProcessorTests()
    {
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantId = Guid.NewGuid();
        _tenantContextMock.Setup(x => x.TenantId).Returns(tenantId);
        _tenantContextMock.Setup(x => x.AllowedTenantIds).Returns(new[] { tenantId });
        _tenantContextMock.Setup(x => x.AllowedBranchIds).Returns(Array.Empty<Guid>());
        _tenantContextMock.Setup(x => x.IsPlatformContext).Returns(true);
        
        _dbContext = new CollectionsDbContext(options, _tenantContextMock.Object);
    }

    [Fact]
    public async Task ProcessMessagesAsync_ShouldMarkAsSent_WhenDispatchSucceeds()
    {
        // Arrange
        var tenantId = _tenantContextMock.Object.TenantId!.Value;
        var receiptId = Guid.NewGuid();
        
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = "ReceiptIssuedEvent",
            Payload = JsonSerializer.Serialize(new { ReceiptId = receiptId, TemplateKey = "test.key" }),
            Status = OutboxMessageStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.OutboxMessages.Add(message);

        var contributor = new Contributor { Id = Guid.NewGuid(), TenantId = tenantId, Name = "John", PhoneNumber = "123", Email = "a@b.com" };
        var fund = new RecipientFund { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Fund" };
        var ev = new Event { Id = Guid.NewGuid(), TenantId = tenantId, Title = "Event" };
        
        var contribution = new Contribution 
        { 
            Id = Guid.NewGuid(),
            TenantId = tenantId, 
            ContributorId = contributor.Id,
            Contributor = contributor,
            ContributorName = "John", 
            Currency = "GHS", 
            Amount = 100,
            EventId = ev.Id,
            Event = ev,
            RecipientFundId = fund.Id,
            RecipientFund = fund
        };

        var receipt = new Receipt 
        { 
            Id = receiptId, 
            TenantId = tenantId, 
            ReceiptNumber = "RCT-1",
            ContributionId = contribution.Id,
            Contribution = contribution,
            EventId = ev.Id,
            Event = ev,
            RecipientFundId = fund.Id,
            RecipientFund = fund
        };

        _dbContext.Contributors.Add(contributor);
        _dbContext.RecipientFunds.Add(fund);
        _dbContext.Events.Add(ev);
        _dbContext.Contributions.Add(contribution);
        _dbContext.Receipts.Add(receipt);
        await _dbContext.SaveChangesAsync();

        _templateResolverMock.Setup(x => x.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationTemplate { Body = "Hello", Subject = "Sub", TenantId = tenantId });
        
        _templateRendererMock.Setup(x => x.Render(It.IsAny<string>(), It.IsAny<object?>()))
            .Returns(new RenderedTemplate("Rendered"));

        _smsServiceMock.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSendResult(true));
        
        _emailServiceMock.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var processor = new OutboxProcessor(_dbContext, _templateResolverMock.Object, _templateRendererMock.Object, _emailServiceMock.Object, _smsServiceMock.Object, _tenantContextMock.Object, _loggerMock.Object);

        // Act
        await processor.ProcessMessagesAsync();

        // Assert
        var updatedMessage = await _dbContext.OutboxMessages.FindAsync(message.Id);
        updatedMessage!.Status.Should().Be(OutboxMessageStatus.Sent, updatedMessage.LastError);
    }

    [Fact]
    public async Task RecoverAbandonedMessagesAsync_ShouldResetStuckMessages()
    {
        // Arrange
        var tenantId = _tenantContextMock.Object.TenantId!.Value;
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Status = OutboxMessageStatus.Processing,
            ProcessedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20),
            Payload = "{}"
        };
        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        var processor = new OutboxProcessor(_dbContext, _templateResolverMock.Object, _templateRendererMock.Object, _emailServiceMock.Object, _smsServiceMock.Object, _tenantContextMock.Object, _loggerMock.Object);

        // Act
        await processor.ProcessMessagesAsync(batchSize: 0); 

        // Assert
        var updatedMessage = await _dbContext.OutboxMessages.FindAsync(message.Id);
        updatedMessage!.Status.Should().Be(OutboxMessageStatus.Failed);
        updatedMessage.LastError.Should().Contain("recovered");
    }
}
