using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Contributions.Commands.RecordCashContribution;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Infrastructure.Payments;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using Xunit;

namespace MFTL.Collections.Infrastructure.Tests.Payments;

public class PaymentIdempotencyTests
{
    [Fact]
    public async Task ProcessAsync_ShouldNotProcessTwice_WhenSameEventId()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: "PaymentIdempotency_" + Guid.NewGuid())
            .Options;

        var loggerMock = new Mock<ILogger<PaymentWebhookProcessor>>();
        var settlementMock = new Mock<IContributionSettlementService>();
        var outboxServiceMock = new Mock<IOutboxService>();
        var providerMock = new Mock<IPaymentProvider>();
        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(t => t.TenantId).Returns(Guid.NewGuid());
        tenantContextMock.Setup(t => t.IsPlatformContext).Returns(false);
        tenantContextMock.Setup(t => t.AllowedTenantIds).Returns(Array.Empty<Guid>());
        tenantContextMock.Setup(t => t.AllowedBranchIds).Returns(Array.Empty<Guid>());
        settlementMock
            .Setup(s => s.SettleContributionAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContributionSettlementResult(Guid.NewGuid(), Guid.NewGuid()));

        providerMock.Setup(p => p.ProviderName).Returns("Stripe");
        providerMock.Setup(p => p.ParseWebhook(It.IsAny<string>()))
            .Returns(new ParsedWebhookResult("evt_123", Guid.NewGuid(), "ref_123", PaymentStatus.Succeeded));

        using (var context = new CollectionsDbContext(options, tenantContextMock.Object))
        {
            var payment = new Payment 
            { 
                Id = Guid.NewGuid(), 
                ContributionId = Guid.NewGuid(), 
                ProviderReference = "ref_123",
                Status = PaymentStatus.Pending
            };
            context.Payments.Add(payment);
            await context.SaveChangesAsync();

            var processor = new PaymentWebhookProcessor(
                context,
                settlementMock.Object,
                outboxServiceMock.Object,
                new[] { providerMock.Object },
                loggerMock.Object);
            
            var payload = "{}";
            var eventId = "evt_123";

            // Act
            await processor.ProcessAsync("Stripe", eventId, payload);
            await processor.ProcessAsync("Stripe", eventId, payload);

            // Assert
            var processedEvents = await context.Set<ProcessedWebhookEvent>().CountAsync();
            Assert.Equal(1, processedEvents);
        }
    }
}
