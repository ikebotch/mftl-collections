using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using MFTL.Collections.Api.Functions.Payments;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;
using MFTL.Collections.Infrastructure.Persistence;

namespace MFTL.Collections.Tests.Api.Functions.Payments;

public class InternalPaymentCallbackFunctionTests : IDisposable
{
    private readonly CollectionsDbContext _dbContext;
    private readonly Mock<IContributionSettlementService> _settlementServiceMock;
    private readonly InternalPaymentCallbackFunction _function;
    private readonly string _sharedSecret = "test-secret-12345678901234567890";
    private readonly Guid _contributionId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public InternalPaymentCallbackFunctionTests()
    {
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(x => x.IsPlatformContext).Returns(true);
        tenantContextMock.Setup(x => x.AllowedTenantIds).Returns(new[] { _tenantId });

        _dbContext = new CollectionsDbContext(options, tenantContextMock.Object);

        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(x => x["Values:Payments:Internal:SharedSecret"]).Returns(_sharedSecret);
        
        _settlementServiceMock = new Mock<IContributionSettlementService>();
        var loggerMock = new Mock<ILogger<InternalPaymentCallbackFunction>>();

        _function = new InternalPaymentCallbackFunction(
            configurationMock.Object,
            _dbContext,
            _settlementServiceMock.Object,
            loggerMock.Object);
            
        SeedDatabase();
    }

    private void SeedDatabase()
    {
        _dbContext.Contributions.Add(new Contribution
        {
            Id = _contributionId,
            TenantId = _tenantId,
            Reference = "REF-VALID",
            Amount = 100.00m,
            Currency = "GHS",
            Status = ContributionStatus.Pending
        });
        
        _dbContext.Contributions.Add(new Contribution
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Reference = "REF-COMPLETED",
            Amount = 100.00m,
            Currency = "GHS",
            Status = ContributionStatus.Completed
        });
        
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private HttpRequest CreateRequest(string body, string? timestamp = null, string? signature = null)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));
        request.Body = stream;
        
        if (timestamp != null) request.Headers["X-MFTL-Timestamp"] = timestamp;
        if (signature != null) request.Headers["X-MFTL-Signature"] = signature;
        
        return request;
    }

    private (string Timestamp, string Signature) GenerateSignature(string body)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_sharedSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return (timestamp, Convert.ToHexString(hash).ToLowerInvariant());
    }

    [Fact]
    public async Task Run_ValidCallback_SettlesContribution()
    {
        var paymentId = Guid.NewGuid().ToString();
        var body = JsonSerializer.Serialize(new
        {
            EventType = "PaymentSucceeded",
            PaymentId = paymentId,
            Provider = "Moolre",
            ProviderReference = "EXT-123",
            ProviderTransactionId = "TXN-123",
            ClientApp = "test",
            ExternalReference = "REF-VALID",
            Amount = 100.00m,
            Currency = "GHS",
            Status = "Successful"
        });

        var (timestamp, signature) = GenerateSignature(body);
        var request = CreateRequest(body, timestamp, signature);

        var result = await _function.Run(request);

        Assert.IsType<OkResult>(result);
        _settlementServiceMock.Verify(x => x.SettleContributionAsync(_contributionId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
        
        var record = await _dbContext.ProcessedExternalPaymentCallbacks.FirstOrDefaultAsync(x => x.PaymentServicePaymentId == paymentId);
        Assert.NotNull(record);
        Assert.Equal("Processed", record.Status);
    }

    [Fact]
    public async Task Run_DuplicateCallback_ReturnsOk_DoesNotDuplicateSettlement()
    {
        var paymentId = Guid.NewGuid().ToString();
        
        // Seed an existing processed callback
        _dbContext.ProcessedExternalPaymentCallbacks.Add(new ProcessedExternalPaymentCallback
        {
            PaymentServicePaymentId = paymentId,
            Provider = "Moolre",
            ExternalReference = "REF-VALID",
            Status = "Processed"
        });
        await _dbContext.SaveChangesAsync();

        var body = JsonSerializer.Serialize(new
        {
            EventType = "PaymentSucceeded",
            PaymentId = paymentId,
            ExternalReference = "REF-VALID",
            Amount = 100.00m,
            Currency = "GHS"
        });

        var (timestamp, signature) = GenerateSignature(body);
        var request = CreateRequest(body, timestamp, signature);

        var result = await _function.Run(request);

        Assert.IsType<OkResult>(result);
        _settlementServiceMock.Verify(x => x.SettleContributionAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_InvalidSignature_ReturnsUnauthorized()
    {
        var body = JsonSerializer.Serialize(new { EventType = "PaymentSucceeded" });
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var request = CreateRequest(body, timestamp, "invalid-signature");

        var result = await _function.Run(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Run_StaleTimestamp_ReturnsUnauthorized()
    {
        var body = JsonSerializer.Serialize(new { EventType = "PaymentSucceeded" });
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-6).ToUnixTimeSeconds().ToString();
        
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_sharedSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();

        var request = CreateRequest(body, timestamp, signature);

        var result = await _function.Run(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Run_AmountMismatch_ReturnsBadRequest_DoesNotSettle()
    {
        var paymentId = Guid.NewGuid().ToString();
        var body = JsonSerializer.Serialize(new
        {
            EventType = "PaymentSucceeded",
            PaymentId = paymentId,
            ExternalReference = "REF-VALID",
            Amount = 50.00m, // Mismatch
            Currency = "GHS"
        });

        var (timestamp, signature) = GenerateSignature(body);
        var request = CreateRequest(body, timestamp, signature);

        var result = await _function.Run(request);

        Assert.IsType<BadRequestObjectResult>(result);
        _settlementServiceMock.Verify(x => x.SettleContributionAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        
        var record = await _dbContext.ProcessedExternalPaymentCallbacks.FirstOrDefaultAsync(x => x.PaymentServicePaymentId == paymentId);
        Assert.NotNull(record);
        Assert.Equal("Rejected", record.Status);
    }

    [Fact]
    public async Task Run_MissingSharedSecret_ReturnsInternalServerError()
    {
        var emptyConfigMock = new Mock<IConfiguration>();
        emptyConfigMock.Setup(x => x["Values:Payments:Internal:SharedSecret"]).Returns((string?)null);
        
        var function = new InternalPaymentCallbackFunction(
            emptyConfigMock.Object,
            _dbContext,
            _settlementServiceMock.Object,
            new Mock<ILogger<InternalPaymentCallbackFunction>>().Object);

        var body = JsonSerializer.Serialize(new { EventType = "PaymentSucceeded" });
        var request = CreateRequest(body, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), "signature");

        var result = await function.Run(request);

        Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(500, ((StatusCodeResult)result).StatusCode);
    }
}
