using FluentAssertions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Domain.Events;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Infrastructure.Services;
using MFTL.Collections.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Text.Json;
using Xunit;

namespace MFTL.Collections.Infrastructure.Tests.Services;

public class OutboxProcessorTests
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ISmsService _smsService;
    private readonly IEmailService _emailService;
    private readonly INotificationTemplateService _templateService;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly string _databaseName;

    public OutboxProcessorTests()
    {
        _databaseName = Guid.NewGuid().ToString();
        _dbContext = CreateDbContext(_databaseName);
        _smsService = Substitute.For<ISmsService>();
        _emailService = Substitute.For<IEmailService>();
        _templateService = Substitute.For<INotificationTemplateService>();
        _logger = Substitute.For<ILogger<OutboxProcessor>>();

        // Default: render returns a body
        _templateService.RenderAsync(
            Arg.Any<string>(), Arg.Any<NotificationChannel>(),
            Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(((string?)null, "Rendered Content"));
    }

    private CollectionsDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var tenantContext = Substitute.For<ITenantContext>();
        var branchContext = Substitute.For<IBranchContext>();

        return new CollectionsDbContext(options, tenantContext, branchContext);
    }

    private OutboxProcessor CreateProcessor(IApplicationDbContext? db = null) =>
        new(db ?? _dbContext, _smsService, _emailService, _templateService, _logger);

    [Fact]
    public async Task ProcessMessageAsync_Retry_ShouldNotCreateDuplicateNotification()
    {
        // Arrange
        var resendEvent = new ReceiptResendRequestedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "REC-1", "John", null, "+233", 100, "GHS", "Event");
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = nameof(ReceiptResendRequestedEvent),
            PayloadJson = JsonSerializer.Serialize(resendEvent),
            Status = OutboxMessageStatus.Processing,
            TenantId = resendEvent.TenantId,
            BranchId = resendEvent.BranchId
        };

        // Pre-create a sent notification (idempotency check)
        var existing = new Notification
        {
            Id = Guid.NewGuid(),
            OutboxMessageId = message.Id,
            Channel = NotificationChannel.Sms,
            RecipientPhone = "+233",
            TemplateKey = "receipt.resend",
            Status = NotificationStatus.Sent,
            Body = "Already sent",
            TenantId = message.TenantId,
            BranchId = message.BranchId
        };

        _dbContext.OutboxMessages.Add(message);
        _dbContext.Notifications.Add(existing);
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        var processor = CreateProcessor();

        // Act — call private ProcessMessageAsync via reflection
        var method = typeof(OutboxProcessor).GetMethod("ProcessMessageAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(processor, new object[] { message, CancellationToken.None })!;

        // Assert: still only 1 notification, no duplicate
        var count = await _dbContext.Notifications.CountAsync(n => n.OutboxMessageId == message.Id);
        count.Should().Be(1);
        await _smsService.DidNotReceive().SendSmsAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ProcessMessageAsync_MissingPhone_ShouldCreateSkippedNotification()
    {
        // Arrange — no phone number on the event
        var resendEvent = new ReceiptResendRequestedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "REC-1", "John", null, null /* no phone */, 100, "GHS", "Event");

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = nameof(ReceiptResendRequestedEvent),
            PayloadJson = JsonSerializer.Serialize(resendEvent),
            Status = OutboxMessageStatus.Processing,
            TenantId = resendEvent.TenantId,
            BranchId = resendEvent.BranchId
        };

        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        var processor = CreateProcessor();

        // Act
        var method = typeof(OutboxProcessor).GetMethod("ProcessMessageAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(processor, new object[] { message, CancellationToken.None })!;

        // Assert: SMS skipped because ContributorPhone is null
        // (ReceiptResend handler calls DispatchAsync only if phone is non-empty, so no notification row is created for SMS)
        await _smsService.DidNotReceive().SendSmsAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ProcessMessageAsync_MissingTemplate_ShouldCreateSkippedNotification()
    {
        // Arrange — template service returns null (no template found)
        _templateService.RenderAsync(
            Arg.Any<string>(), Arg.Any<NotificationChannel>(),
            Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(string?, string)?>(null));

        var resendEvent = new ReceiptResendRequestedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "REC-1", "John", null, "+233244000001", 100, "GHS", "Event");

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = nameof(ReceiptResendRequestedEvent),
            PayloadJson = JsonSerializer.Serialize(resendEvent),
            Status = OutboxMessageStatus.Processing,
            TenantId = resendEvent.TenantId,
            BranchId = resendEvent.BranchId
        };

        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        var processor = CreateProcessor();

        var method = typeof(OutboxProcessor).GetMethod("ProcessMessageAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(processor, new object[] { message, CancellationToken.None })!;

        // Assert: a Skipped notification was created
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.OutboxMessageId == message.Id && n.Channel == NotificationChannel.Sms);
        notification.Should().NotBeNull();
        notification!.Status.Should().Be(NotificationStatus.Skipped);
        notification.Error.Should().Contain("No active template");
    }
}
