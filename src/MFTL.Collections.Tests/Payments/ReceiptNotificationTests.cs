using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Infrastructure.Persistence;
using MFTL.Collections.Infrastructure.Services;
using MFTL.Collections.Infrastructure.Tenancy;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Http;
using MFTL.Collections.Domain.Common;
using MFTL.Collections.Application.Features.Receipts.Queries.GetReceiptById;
using System.Text.Json;

namespace MFTL.Collections.Tests.Payments;

public class ReceiptNotificationTests
{
    private readonly CollectionsDbContext _dbContext;
    private readonly ContributionSettlementService _settlementService;
    private readonly Mock<ITenantContext> _tenantContextMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IReceiptNumberGenerator> _receiptNumberGeneratorMock = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();

    public ReceiptNotificationTests()
    {
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .AddInterceptors(new OutboxInterceptor(_httpContextAccessorMock.Object))
            .Options;

        _tenantContextMock.Setup(t => t.IsSystemContext).Returns(true);
        _tenantContextMock.Setup(t => t.TenantId).Returns(Guid.NewGuid());
        _dbContext = new CollectionsDbContext(options, _tenantContextMock.Object, _currentUserServiceMock.Object);

        _receiptNumberGeneratorMock.Setup(x => x.Generate()).Returns("RCT-123456");
        
        _settlementService = new ContributionSettlementService(
            _dbContext,
            _currentUserServiceMock.Object,
            _receiptNumberGeneratorMock.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<ContributionSettlementService>>().Object);
    }

    [Fact]
    public async Task SettleContribution_Should_Queue_ReceiptIssuedEvent_For_Cash()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var contribution = await CreateContributionAsync(tenantId, "Cash");

        // Act
        await _settlementService.SettleContributionAsync(contribution, null);
        await _dbContext.SaveChangesAsync();

        // Assert
        var outboxMessage = await _dbContext.OutboxMessages.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.EventType == "ReceiptIssuedEvent");
        Assert.NotNull(outboxMessage);
        Assert.Contains("receipt.issued", outboxMessage.Payload);
    }

    [Fact]
    public async Task SettleContribution_Should_Queue_ReceiptIssuedEvent_For_GoCardless()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var contribution = await CreateContributionAsync(tenantId, "GoCardless");
        var payment = new Payment 
        { 
            Id = Guid.NewGuid(), 
            TenantId = tenantId, 
            ContributionId = contribution.Id, 
            Status = PaymentStatus.Succeeded, 
            Method = "GoCardless",
            Amount = contribution.Amount,
            Currency = contribution.Currency
        };
        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync();

        // Act
        await _settlementService.SettleContributionAsync(contribution, payment.Id);
        await _dbContext.SaveChangesAsync();

        // Assert
        var outboxMessage = await _dbContext.OutboxMessages.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.EventType == "ReceiptIssuedEvent");
        Assert.NotNull(outboxMessage);
        Assert.Contains("receipt.issued", outboxMessage.Payload);
    }

    [Fact]
    public async Task GetReceiptById_Should_Return_Mapped_PaymentMethod()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var contribution = await CreateContributionAsync(tenantId, "moolre");
        var payment = new Payment 
        { 
            Id = Guid.NewGuid(), 
            TenantId = tenantId, 
            ContributionId = contribution.Id, 
            Status = PaymentStatus.Succeeded, 
            Method = "moolre",
            Amount = contribution.Amount,
            Currency = contribution.Currency
        };
        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync();

        await _settlementService.SettleContributionAsync(contribution, payment.Id);
        await _dbContext.SaveChangesAsync();

        _tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);
        _tenantContextMock.Setup(t => t.IsPlatformContext).Returns(true);
        
        var query = new GetReceiptByIdQuery(contribution.Receipt!.Id);
        var handler = new GetReceiptByIdQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(query, default);

        // Assert
        Assert.Equal("Mobile Money", result.PaymentMethod);
    }

    private async Task<Contribution> CreateContributionAsync(Guid tenantId, string method)
    {
        var ev = new Event { Id = Guid.NewGuid(), TenantId = tenantId, Title = "Test Event", BranchId = Guid.NewGuid() };
        var fund = new RecipientFund { Id = Guid.NewGuid(), TenantId = tenantId, EventId = ev.Id, Name = "Test Fund", BranchId = ev.BranchId };
        _dbContext.Events.Add(ev);
        _dbContext.RecipientFunds.Add(fund);

        var contribution = new Contribution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = ev.BranchId,
            Amount = 100,
            Currency = "GHS",
            Reference = "REF-" + Guid.NewGuid().ToString("N")[..8],
            Status = ContributionStatus.AwaitingPayment,
            EventId = ev.Id,
            RecipientFundId = fund.Id,
            ContributorName = "Test Donor",
            Method = method
        };
        _dbContext.Contributions.Add(contribution);
        await _dbContext.SaveChangesAsync();
        return contribution;
    }
}
