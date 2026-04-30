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

namespace MFTL.Collections.Tests.Infrastructure.Services;

public class ContributionSettlementServiceTests
{
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IReceiptNumberGenerator> _receiptNumberGeneratorMock = new();
    private readonly Mock<ILogger<ContributionSettlementService>> _loggerMock = new();
    private readonly Mock<ITenantContext> _tenantContextMock = new();
    private readonly CollectionsDbContext _dbContext;

    public ContributionSettlementServiceTests()
    {
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantId = Guid.NewGuid();
        _tenantContextMock.Setup(x => x.TenantId).Returns(tenantId);
        _tenantContextMock.Setup(x => x.IsPlatformContext).Returns(true);
        _tenantContextMock.Setup(x => x.AllowedTenantIds).Returns(new[] { tenantId });
        _tenantContextMock.Setup(x => x.AllowedBranchIds).Returns(Array.Empty<Guid>());

        _dbContext = new CollectionsDbContext(options, _tenantContextMock.Object);
        _receiptNumberGeneratorMock.Setup(x => x.Generate()).Returns("RCT-123");
    }

    [Fact]
    public async Task SettleContributionAsync_ShouldCreateReceipt_WhenNotExists()
    {
        // Arrange
        var tenantId = _tenantContextMock.Object.TenantId!.Value;
        var branchId = Guid.NewGuid();
        var fund = new RecipientFund { Id = Guid.NewGuid(), TenantId = tenantId, BranchId = branchId, Name = "Test Fund" };
        var contribution = new Contribution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = branchId,
            RecipientFundId = fund.Id,
            Amount = 100,
            Status = ContributionStatus.Pending
        };
        _dbContext.RecipientFunds.Add(fund);
        _dbContext.Contributions.Add(contribution);
        await _dbContext.SaveChangesAsync();

        var service = new ContributionSettlementService(_dbContext, _currentUserServiceMock.Object, _receiptNumberGeneratorMock.Object, _loggerMock.Object);

        // Act
        var result = await service.SettleContributionAsync(contribution.Id, null);

        // Assert
        result.Should().NotBeNull();
        var receipt = await _dbContext.Receipts.FirstOrDefaultAsync(r => r.ContributionId == contribution.Id);
        receipt.Should().NotBeNull();
        receipt!.TenantId.Should().Be(tenantId);
        receipt.BranchId.Should().Be(branchId);
        contribution.Status.Should().Be(ContributionStatus.Completed);
        fund.CollectedAmount.Should().Be(100);
    }

    [Fact]
    public async Task SettleContributionAsync_ShouldReuseExistingReceipt_WhenAlreadyExists()
    {
        // Arrange
        var tenantId = _tenantContextMock.Object.TenantId!.Value;
        var fund = new RecipientFund { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Test Fund" };
        var contribution = new Contribution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RecipientFundId = fund.Id,
            Amount = 100,
            Status = ContributionStatus.Completed
        };
        var existingReceipt = new Receipt
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContributionId = contribution.Id,
            ReceiptNumber = "OLD-RCT"
        };
        _dbContext.RecipientFunds.Add(fund);
        _dbContext.Contributions.Add(contribution);
        _dbContext.Receipts.Add(existingReceipt);
        await _dbContext.SaveChangesAsync();

        var service = new ContributionSettlementService(_dbContext, _currentUserServiceMock.Object, _receiptNumberGeneratorMock.Object, _loggerMock.Object);

        // Act
        var result = await service.SettleContributionAsync(contribution.Id, null);

        // Assert
        result.ReceiptId.Should().Be(existingReceipt.Id);
        var receiptCount = await _dbContext.Receipts.CountAsync(r => r.ContributionId == contribution.Id);
        receiptCount.Should().Be(1);
    }

    [Fact]
    public async Task SettleContributionAsync_ShouldHandleCashWithNullPaymentId()
    {
        // Arrange
        var tenantId = _tenantContextMock.Object.TenantId!.Value;
        var fund = new RecipientFund { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Test Fund" };
        var contribution = new Contribution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RecipientFundId = fund.Id,
            Amount = 50,
            Method = "Cash",
            Status = ContributionStatus.Pending
        };
        _dbContext.RecipientFunds.Add(fund);
        _dbContext.Contributions.Add(contribution);
        await _dbContext.SaveChangesAsync();

        var service = new ContributionSettlementService(_dbContext, _currentUserServiceMock.Object, _receiptNumberGeneratorMock.Object, _loggerMock.Object);

        // Act
        var result = await service.SettleContributionAsync(contribution.Id, null);

        // Assert
        result.Should().NotBeNull();
        contribution.Status.Should().Be(ContributionStatus.Completed);
    }
}
