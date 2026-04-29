using FluentAssertions;
using MFTL.Collections.Application.Features.Receipts.Commands.ResendReceipt;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using MFTL.Collections.Application.Tests.Users; 
using MFTL.Collections.Application.Common.Interfaces;
using Xunit;

namespace MFTL.Collections.Application.Tests.Features.Receipts;

public class ResendReceiptTests
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<ResendReceiptCommandHandler> _logger;

    public ResendReceiptTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TestDbContext(options);
        _logger = Substitute.For<ILogger<ResendReceiptCommandHandler>>();
    }

    [Fact]
    public async Task Handle_ShouldRaiseReceiptResendRequestedEvent_WithMetadata()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        
        var @event = new Event { Id = eventId, Title = "Annual Rally", TenantId = tenantId, BranchId = branchId };
        var contributor = new Contributor { Id = Guid.NewGuid(), Name = "John Doe", PhoneNumber = "+233240000000", TenantId = tenantId, BranchId = branchId };
        var contribution = new Contribution 
        { 
            Id = Guid.NewGuid(), 
            Amount = 100, 
            Currency = "GHS", 
            Contributor = contributor, 
            ContributorName = contributor.Name,
            Event = @event,
            EventId = eventId,
            TenantId = tenantId,
            BranchId = branchId
        };
        var receipt = new Receipt 
        { 
            Id = Guid.NewGuid(), 
            ReceiptNumber = "REC-001", 
            Contribution = contribution,
            ContributionId = contribution.Id,
            TenantId = tenantId,
            BranchId = branchId
        };

        _dbContext.Receipts.Add(receipt);
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        var command = new ResendReceiptCommand(receipt.Id);
        var handler = new ResendReceiptCommandHandler(_dbContext, _logger);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        
        var updatedReceipt = await _dbContext.Receipts.FirstAsync(r => r.Id == receipt.Id);
        var resendEvent = updatedReceipt.DomainEvents.OfType<ReceiptResendRequestedEvent>().FirstOrDefault();
        
        resendEvent.Should().NotBeNull();
        resendEvent!.ReceiptId.Should().Be(receipt.Id);
        resendEvent.TenantId.Should().Be(tenantId);
        resendEvent.BranchId.Should().Be(branchId);
        resendEvent.AggregateId.Should().Be(receipt.Id);
    }
}
