using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MFTL.Collections.Application.Common.Exceptions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Features.Collectors.Queries.GetContributionStatus;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Infrastructure.Persistence;
using Moq;
using Xunit;
using MediatR;

namespace MFTL.Collections.Tests.Features.Collectors;

public class GetContributionStatusTests : IDisposable
{
    private readonly CollectionsDbContext _dbContext;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<IScopeAccessService> _scopeAccessServiceMock;
    private readonly IMediator _mediator;

    public GetContributionStatusTests()
    {
        var services = new ServiceCollection();
        
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _tenantContextMock = new Mock<ITenantContext>();
        _scopeAccessServiceMock = new Mock<IScopeAccessService>();
        _dbContext = new CollectionsDbContext(options, _tenantContextMock.Object, _currentUserServiceMock.Object);
        
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetContributionStatusQuery).Assembly));
        services.AddLogging();
        services.AddSingleton<IApplicationDbContext>(_dbContext);
        services.AddSingleton(_currentUserServiceMock.Object);
        services.AddSingleton(_tenantContextMock.Object);
        services.AddSingleton(_scopeAccessServiceMock.Object);
        
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task Handle_Returns_Status_For_Assigned_Event()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var contribution = new Contribution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventId = eventId,
            RecipientFundId = Guid.NewGuid(),
            Amount = 100,
            Currency = "GHS",
            Status = ContributionStatus.Pending,
            Method = "MoMo"
        };
        _tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);
        _dbContext.Contributions.Add(contribution);
        await _dbContext.SaveChangesAsync();

        _scopeAccessServiceMock.Setup(s => s.CanAccessAsync(Permissions.Contributions.Create, tenantId, null, eventId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var query = new GetContributionStatusQuery(contribution.Id);

        // Act
        var result = await _mediator.Send(query);

        // Assert
        Assert.Equal(contribution.Id, result.ContributionId);
        Assert.Equal("Pending", result.Status);
    }

    [Fact]
    public async Task Handle_Throws_Forbidden_For_Other_Tenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var contribution = new Contribution
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            EventId = Guid.NewGuid(),
            RecipientFundId = Guid.NewGuid(),
            Status = ContributionStatus.Pending
        };
        _tenantContextMock.Setup(t => t.TenantId).Returns(otherTenantId);
        _dbContext.Contributions.Add(contribution);
        await _dbContext.SaveChangesAsync();

        _tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);

        var query = new GetContributionStatusQuery(contribution.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _mediator.Send(query));
    }

    [Fact]
    public async Task Handle_Throws_Forbidden_For_Unassigned_Collector()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var contribution = new Contribution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventId = eventId,
            RecipientFundId = Guid.NewGuid(),
            Status = ContributionStatus.Pending
        };
        _tenantContextMock.Setup(t => t.IsSystemContext).Returns(true);
        _dbContext.Contributions.Add(contribution);
        await _dbContext.SaveChangesAsync();
        _tenantContextMock.Setup(t => t.IsSystemContext).Returns(false);

        _tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);

        var query = new GetContributionStatusQuery(contribution.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => _mediator.Send(query));
    }

    [Fact]
    public async Task Handle_Returns_ReceiptId_When_Completed()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var contributionId = Guid.NewGuid();
        var contribution = new Contribution
        {
            Id = contributionId,
            TenantId = tenantId,
            EventId = eventId,
            RecipientFundId = Guid.NewGuid(),
            Amount = 100,
            Currency = "GHS",
            Status = ContributionStatus.Completed,
            Method = "MoMo"
        };
        _tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);
        _dbContext.Contributions.Add(contribution);
        
        var receipt = new Receipt
        {
            Id = receiptId,
            TenantId = tenantId,
            ContributionId = contributionId,
            EventId = eventId,
            RecipientFundId = contribution.RecipientFundId,
            BranchId = Guid.NewGuid(),
            ReceiptNumber = "R-123"
        };
        _dbContext.Receipts.Add(receipt);
        
        await _dbContext.SaveChangesAsync();

        _scopeAccessServiceMock.Setup(s => s.CanAccessAsync(Permissions.Contributions.Create, tenantId, null, eventId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var query = new GetContributionStatusQuery(contribution.Id);

        // Act
        var result = await _mediator.Send(query);

        // Assert
        Assert.Equal(receiptId, result.ReceiptId);
    }
}
