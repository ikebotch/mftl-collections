using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Api.Functions.Payments;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Infrastructure.Tenancy;
using MFTL.Collections.Infrastructure.Persistence;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Tests.Payments;

public class InternalPaymentCallbackTests : IDisposable
{
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<IContributionSettlementService> _settlementServiceMock = new();
    private readonly Mock<ITenantContext> _tenantContextMock = new();
    private readonly Mock<ILogger<InternalPaymentCallbackFunction>> _loggerMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly CollectionsDbContext _dbContext;
    private readonly InternalPaymentCallbackFunction _function;
    private const string SharedSecret = "test-secret-12345678901234567890";

    public InternalPaymentCallbackTests()
    {
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        // Default to system context for test setup
        _tenantContextMock.Setup(t => t.IsSystemContext).Returns(true);
        
        _dbContext = new CollectionsDbContext(options, _tenantContextMock.Object, _currentUserServiceMock.Object);
        
        _configurationMock.Setup(x => x["Payments:Internal:SharedSecret"]).Returns(SharedSecret);
        
        _function = new InternalPaymentCallbackFunction(
            _configurationMock.Object,
            _dbContext,
            _settlementServiceMock.Object,
            _tenantContextMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private static string ComputeSignature(string secret, string timestamp, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public async Task Should_Return_Ok_And_Settle_Contribution_When_Valid_Succeeded_Callback()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var contributionId = Guid.NewGuid();
        var amount = 10.0m;
        var currency = "GHS";
        var reference = "REF-123";

        var ev = new Event { Id = Guid.NewGuid(), TenantId = tenantId, Title = "Test Event" };
        var fund = new RecipientFund { Id = Guid.NewGuid(), TenantId = tenantId, EventId = ev.Id, Name = "Test Fund" };
        _dbContext.Events.Add(ev);
        _dbContext.RecipientFunds.Add(fund);

        var contribution = new Contribution
        {
            Id = contributionId,
            TenantId = tenantId,
            Amount = amount,
            Currency = currency,
            Reference = reference,
            Status = ContributionStatus.AwaitingPayment,
            EventId = ev.Id,
            RecipientFundId = fund.Id,
            ContributorName = "Test Donor"
        };
        _dbContext.Contributions.Add(contribution);
        await _dbContext.SaveChangesAsync();

        var payload = new
        {
            CallbackEventId = "EVT-1",
            EventType = "PaymentSucceeded",
            PaymentServicePaymentId = "PAY-1",
            TenantId = tenantId,
            ContributionId = contributionId,
            Provider = "moolre",
            ProviderReference = "MOCK-REF",
            ExternalReference = reference,
            Amount = amount,
            Currency = currency,
            OccurredAt = DateTimeOffset.UtcNow
        };

        var body = JsonSerializer.Serialize(payload);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = ComputeSignature(SharedSecret, timestamp, body);

        var req = new DefaultHttpContext().Request;
        req.Headers["X-MFTL-Timestamp"] = timestamp;
        req.Headers["X-MFTL-Signature"] = signature;
        req.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        // Act
        var result = await _function.Run(req);

        // Assert
        Assert.IsType<OkResult>(result);
        
        var processedCallback = await _dbContext.ProcessedExternalPaymentCallbacks.FirstOrDefaultAsync(x => x.CallbackEventId == "EVT-1");
        Assert.NotNull(processedCallback);
        Assert.Equal("Processed", processedCallback.Status);

        _settlementServiceMock.Verify(x => x.SettleContributionAsync(
            It.Is<Contribution>(c => c.Id == contributionId), 
            null, 
            null, 
            default), Times.Once);
    }

    [Fact]
    public async Task Should_Return_Ok_And_Be_Idempotent_On_Duplicate_Callback()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var contributionId = Guid.NewGuid();
        
        _dbContext.ProcessedExternalPaymentCallbacks.Add(new ProcessedExternalPaymentCallback
        {
            CallbackEventId = "EVT-DUPE",
            Status = "Processed",
            TenantId = tenantId,
            ContributionId = contributionId,
            ProcessedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var payload = new
        {
            CallbackEventId = "EVT-DUPE",
            EventType = "PaymentSucceeded",
            PaymentServicePaymentId = "PAY-1",
            TenantId = tenantId,
            ContributionId = contributionId,
            ExternalReference = "REF-123",
            Amount = 10.0m,
            Currency = "GHS"
        };

        var body = JsonSerializer.Serialize(payload);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = ComputeSignature(SharedSecret, timestamp, body);

        var req = new DefaultHttpContext().Request;
        req.Headers["X-MFTL-Timestamp"] = timestamp;
        req.Headers["X-MFTL-Signature"] = signature;
        req.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        // Act
        var result = await _function.Run(req);

        // Assert
        Assert.IsType<OkResult>(result);
        _settlementServiceMock.Verify(x => x.SettleContributionAsync(It.IsAny<Contribution>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
