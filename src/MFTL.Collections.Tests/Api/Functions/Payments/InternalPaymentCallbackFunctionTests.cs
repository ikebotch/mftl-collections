using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using MFTL.Collections.Api.Functions.Payments;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Infrastructure.Services;
using Xunit;

namespace MFTL.Collections.Tests.Api.Functions.Payments;

public class InternalPaymentCallbackFunctionTests : IDisposable
{
    private readonly CollectionsDbContext _dbContext;
    private readonly InternalPaymentCallbackFunction _function;
    private readonly string _sharedSecret = "test-secret-12345678901234567890";
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _fundId = Guid.NewGuid();
    private readonly Guid _contributionId = Guid.NewGuid();
    private readonly Guid _paymentId = Guid.NewGuid();

    public InternalPaymentCallbackFunctionTests()
    {
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(x => x.TenantId).Returns(_tenantId);
        tenantContextMock.Setup(x => x.BranchId).Returns(_branchId);
        tenantContextMock.Setup(x => x.IsPlatformContext).Returns(true);
        tenantContextMock.Setup(x => x.IsSystemContext).Returns(true);
        tenantContextMock.Setup(x => x.AllowedTenantIds).Returns(new[] { _tenantId });
        tenantContextMock.Setup(x => x.AllowedBranchIds).Returns(new[] { _branchId });

        _dbContext = new CollectionsDbContext(options, tenantContextMock.Object, Mock.Of<ICurrentUserService>());

        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(x => x["Values:Payments:Internal:SharedSecret"]).Returns(_sharedSecret);

        var settlementService = new ContributionSettlementService(
            _dbContext,
            Mock.Of<ICurrentUserService>(),
            new SequentialReceiptNumberGenerator(),
            Mock.Of<ILogger<ContributionSettlementService>>());

        _function = new InternalPaymentCallbackFunction(
            configurationMock.Object,
            _dbContext,
            settlementService,
            tenantContextMock.Object,
            Mock.Of<ILogger<InternalPaymentCallbackFunction>>());

        SeedDatabase();
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Run_ValidSignedSuccess_SettlesContributionAndCreatesOneReceipt()
    {
        var body = CreatePayloadJson("evt-success-1", "PaymentSucceeded");
        var request = SignedRequest(body);

        var result = await _function.Run(request);

        Assert.IsType<OkResult>(result);
        var contribution = await _dbContext.Contributions.Include(c => c.Receipt).SingleAsync(c => c.Id == _contributionId);
        Assert.Equal(ContributionStatus.Completed, contribution.Status);
        Assert.NotNull(contribution.Receipt);
        Assert.Equal(100m, (await _dbContext.RecipientFunds.SingleAsync(f => f.Id == _fundId)).CollectedAmount);
        Assert.Equal(1, await _dbContext.Receipts.CountAsync(r => r.ContributionId == _contributionId));
        Assert.Equal("Processed", (await _dbContext.ProcessedExternalPaymentCallbacks.SingleAsync()).Status);
    }

    [Fact]
    public async Task Run_DuplicateSignedSuccess_DoesNotDuplicateReceipt()
    {
        var body = CreatePayloadJson("evt-success-duplicate", "PaymentSucceeded");

        Assert.IsType<OkResult>(await _function.Run(SignedRequest(body)));
        Assert.IsType<OkResult>(await _function.Run(SignedRequest(body)));

        Assert.Equal(1, await _dbContext.Receipts.CountAsync(r => r.ContributionId == _contributionId));
        Assert.Equal(100m, (await _dbContext.RecipientFunds.SingleAsync(f => f.Id == _fundId)).CollectedAmount);
    }

    [Fact]
    public async Task Run_InvalidSignature_ReturnsUnauthorizedAndDoesNotMutate()
    {
        var body = CreatePayloadJson("evt-bad-signature", "PaymentSucceeded");
        var request = CreateRequest(body, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), "invalid-signature");

        var result = await _function.Run(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
        await AssertNoMutationAsync();
    }

    [Fact]
    public async Task Run_StaleTimestamp_ReturnsUnauthorizedAndDoesNotMutate()
    {
        var body = CreatePayloadJson("evt-stale", "PaymentSucceeded");
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-6).ToUnixTimeSeconds().ToString();
        var signature = ComputeSignature(timestamp, body);

        var result = await _function.Run(CreateRequest(body, timestamp, signature));

        Assert.IsType<UnauthorizedObjectResult>(result);
        await AssertNoMutationAsync();
    }

    [Fact]
    public async Task Run_MissingTenantId_ReturnsBadRequest()
    {
        var body = CreatePayloadJson("evt-missing-tenant", "PaymentSucceeded", includeTenantId: false);

        var result = await _function.Run(SignedRequest(body));

        Assert.IsType<BadRequestObjectResult>(result);
        await AssertNoMutationAsync();
    }

    [Fact]
    public async Task Run_MissingContributionId_ReturnsBadRequest()
    {
        var body = CreatePayloadJson("evt-missing-contribution", "PaymentSucceeded", includeContributionId: false);

        var result = await _function.Run(SignedRequest(body));

        Assert.IsType<BadRequestObjectResult>(result);
        await AssertNoMutationAsync();
    }

    [Fact]
    public async Task Run_CrossTenantCallback_ReturnsBadRequestAndDoesNotSettle()
    {
        var body = CreatePayloadJson("evt-cross-tenant", "PaymentSucceeded", tenantId: Guid.NewGuid());

        var result = await _function.Run(SignedRequest(body));

        Assert.IsType<BadRequestObjectResult>(result);
        await AssertNoSettlementAsync();
        Assert.Equal("Rejected", (await _dbContext.ProcessedExternalPaymentCallbacks.SingleAsync()).Status);
    }

    [Fact]
    public async Task Run_AmountMismatch_ReturnsBadRequestAndDoesNotSettle()
    {
        var body = CreatePayloadJson("evt-amount-mismatch", "PaymentSucceeded", amount: 99.99m);

        var result = await _function.Run(SignedRequest(body));

        Assert.IsType<BadRequestObjectResult>(result);
        await AssertNoSettlementAsync();
    }

    [Fact]
    public async Task Run_CurrencyMismatch_ReturnsBadRequestAndDoesNotSettle()
    {
        var body = CreatePayloadJson("evt-currency-mismatch", "PaymentSucceeded", currency: "USD");

        var result = await _function.Run(SignedRequest(body));

        Assert.IsType<BadRequestObjectResult>(result);
        await AssertNoSettlementAsync();
    }

    [Fact]
    public async Task Run_UnknownEventType_ReturnsBadRequest()
    {
        var body = CreatePayloadJson("evt-unknown", "PaymentExpired");

        var result = await _function.Run(SignedRequest(body));

        Assert.IsType<BadRequestObjectResult>(result);
        await AssertNoSettlementAsync();
        Assert.Equal("Rejected", (await _dbContext.ProcessedExternalPaymentCallbacks.SingleAsync()).Status);
    }

    [Fact]
    public async Task Run_FailedCallback_MarksPendingContributionAndPaymentFailed()
    {
        var body = CreatePayloadJson("evt-failed", "PaymentFailed", status: "Failed");

        var result = await _function.Run(SignedRequest(body));

        Assert.IsType<OkResult>(result);
        Assert.Equal(ContributionStatus.Failed, (await _dbContext.Contributions.SingleAsync(c => c.Id == _contributionId)).Status);
        Assert.Equal(PaymentStatus.Failed, (await _dbContext.Payments.SingleAsync(p => p.Id == _paymentId)).Status);
        Assert.Empty(_dbContext.Receipts);
    }

    [Fact]
    public async Task Run_FailedAfterSuccess_DoesNotDowngradeSettledContribution()
    {
        var success = CreatePayloadJson("evt-success-before-fail", "PaymentSucceeded");
        Assert.IsType<OkResult>(await _function.Run(SignedRequest(success)));

        var failed = CreatePayloadJson("evt-failed-after-success", "PaymentFailed", status: "Failed");
        var result = await _function.Run(SignedRequest(failed));

        Assert.IsType<OkResult>(result);
        Assert.Equal(ContributionStatus.Completed, (await _dbContext.Contributions.SingleAsync(c => c.Id == _contributionId)).Status);
        Assert.Equal(PaymentStatus.Succeeded, (await _dbContext.Payments.SingleAsync(p => p.Id == _paymentId)).Status);
        Assert.Equal(1, await _dbContext.Receipts.CountAsync(r => r.ContributionId == _contributionId));
    }

    [Fact]
    public async Task Run_GoCardlessSignedSuccess_UsesExistingSettlementPath()
    {
        var body = CreatePayloadJson("evt-gocardless-success", "PaymentSucceeded", provider: "GoCardless", currency: "GHS");
        var request = SignedRequest(body);

        var result = await _function.Run(request);

        Assert.IsType<OkResult>(result);
        Assert.Equal(ContributionStatus.Completed, (await _dbContext.Contributions.SingleAsync(c => c.Id == _contributionId)).Status);
        Assert.Equal(PaymentStatus.Succeeded, (await _dbContext.Payments.SingleAsync(p => p.Id == _paymentId)).Status);
        Assert.Equal(1, await _dbContext.Receipts.CountAsync(r => r.ContributionId == _contributionId));
    }

    [Fact]
    public async Task Run_MollieSignedSuccess_UsesExistingSettlementPath()
    {
        var body = CreatePayloadJson("evt-mollie-success", "PaymentSucceeded", provider: "Mollie");
        var request = SignedRequest(body);

        var result = await _function.Run(request);

        Assert.IsType<OkResult>(result);
        Assert.Equal(ContributionStatus.Completed, (await _dbContext.Contributions.SingleAsync(c => c.Id == _contributionId)).Status);
        Assert.Equal(PaymentStatus.Succeeded, (await _dbContext.Payments.SingleAsync(p => p.Id == _paymentId)).Status);
        Assert.Equal(1, await _dbContext.Receipts.CountAsync(r => r.ContributionId == _contributionId));
    }

    [Fact]
    public async Task Run_MollieFailedOrCancelledCallback_DoesNotCreateReceipt()
    {
        var body = CreatePayloadJson("evt-mollie-cancelled", "PaymentFailed", status: "Cancelled", provider: "Mollie");
        var request = SignedRequest(body);

        var result = await _function.Run(request);

        Assert.IsType<OkResult>(result);
        Assert.NotEqual(ContributionStatus.Completed, (await _dbContext.Contributions.SingleAsync(c => c.Id == _contributionId)).Status);
        Assert.Empty(_dbContext.Receipts);
    }

    private void SeedDatabase()
    {
        _dbContext.Branches.Add(new Branch { Id = _branchId, TenantId = _tenantId, Name = "Main", Identifier = "main" });
        _dbContext.Events.Add(new Event { Id = _eventId, TenantId = _tenantId, BranchId = _branchId, Title = "Event", Slug = "event" });
        _dbContext.RecipientFunds.Add(new RecipientFund
        {
            Id = _fundId,
            TenantId = _tenantId,
            BranchId = _branchId,
            EventId = _eventId,
            Name = "Fund",
            Description = "Fund",
            TargetAmount = 1000m
        });
        _dbContext.Contributions.Add(new Contribution
        {
            Id = _contributionId,
            TenantId = _tenantId,
            BranchId = _branchId,
            EventId = _eventId,
            RecipientFundId = _fundId,
            PaymentId = _paymentId,
            Reference = "REF-VALID",
            Amount = 100m,
            Currency = "GHS",
            ContributorName = "Donor",
            Method = "Card",
            Status = ContributionStatus.Pending
        });
        _dbContext.Payments.Add(new Payment
        {
            Id = _paymentId,
            TenantId = _tenantId,
            ContributionId = _contributionId,
            Amount = 100m,
            Currency = "GHS",
            Method = "Card",
            ProviderReference = "provider-ref",
            Status = PaymentStatus.Initiated
        });
        _dbContext.SaveChanges();
    }

    private HttpRequest SignedRequest(string body)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        return CreateRequest(body, timestamp, ComputeSignature(timestamp, body));
    }

    private static HttpRequest CreateRequest(string body, string? timestamp = null, string? signature = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (timestamp != null) context.Request.Headers["X-MFTL-Timestamp"] = timestamp;
        if (signature != null) context.Request.Headers["X-MFTL-Signature"] = signature;
        return context.Request;
    }

    private string ComputeSignature(string timestamp, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_sharedSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string CreatePayloadJson(
        string callbackEventId,
        string eventType,
        Guid? tenantId = default,
        Guid? contributionId = default,
        bool includeTenantId = true,
        bool includeContributionId = true,
        decimal amount = 100m,
        string currency = "GHS",
        string status = "Succeeded",
        string provider = "Stripe")
    {
        var effectiveTenantId = tenantId == default ? _tenantId : tenantId;
        var effectiveContributionId = contributionId == default ? _contributionId : contributionId;

        return JsonSerializer.Serialize(new
        {
            callbackEventId,
            eventType,
            paymentServicePaymentId = _paymentId.ToString(),
            tenantId = includeTenantId ? effectiveTenantId : null,
            contributionId = includeContributionId ? effectiveContributionId : null,
            provider,
            providerReference = "provider-ref",
            providerTransactionId = "txn-123",
            clientApp = "mftl-collections",
            externalReference = "REF-VALID",
            amount,
            currency,
            status,
            occurredAt = DateTimeOffset.UtcNow
        });
    }

    private async Task AssertNoMutationAsync()
    {
        Assert.Empty(_dbContext.ProcessedExternalPaymentCallbacks);
        await AssertNoSettlementAsync();
    }

    private async Task AssertNoSettlementAsync()
    {
        Assert.Equal(ContributionStatus.Pending, (await _dbContext.Contributions.SingleAsync(c => c.Id == _contributionId)).Status);
        Assert.Equal(PaymentStatus.Initiated, (await _dbContext.Payments.SingleAsync(p => p.Id == _paymentId)).Status);
        Assert.Empty(_dbContext.Receipts);
        Assert.Equal(0m, (await _dbContext.RecipientFunds.SingleAsync(f => f.Id == _fundId)).CollectedAmount);
    }

    private sealed class SequentialReceiptNumberGenerator : IReceiptNumberGenerator
    {
        private int _next = 1;
        public string Generate() => $"RCT-TEST-{_next++:0000}";
    }
}
