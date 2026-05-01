using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Collectors.Commands.SetupPin;
using MFTL.Collections.Application.Features.Collectors.Commands.VerifyPin;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Infrastructure.Persistence;
using Moq;
using Xunit;
using MediatR;

namespace MFTL.Collections.Tests.Features.Collectors;

public class CollectorPinTests : IDisposable
{
    private readonly CollectionsDbContext _dbContext;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly IMediator _mediator;

    public CollectorPinTests()
    {
        var services = new ServiceCollection();
        
        var options = new DbContextOptionsBuilder<CollectionsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _tenantContextMock = new Mock<ITenantContext>();
        _dbContext = new CollectionsDbContext(options, _tenantContextMock.Object, _currentUserServiceMock.Object);
        
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SetupCollectorPinCommand).Assembly));
        services.AddLogging();
        services.AddSingleton<IApplicationDbContext>(_dbContext);
        services.AddSingleton(_currentUserServiceMock.Object);
        services.AddSingleton(_tenantContextMock.Object);
        
        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _serviceProvider.Dispose();
    }

    private async Task<User> CreateUser(string auth0Id)
    {
        var user = new User { Auth0Id = auth0Id, Email = "test@example.com" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Setup_Requires_Tenant()
    {
        // Arrange
        _currentUserServiceMock.Setup(u => u.UserId).Returns("auth0|123");
        _tenantContextMock.Setup(t => t.TenantId).Returns((Guid?)null);
        var command = new SetupCollectorPinCommand("1234");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _mediator.Send(command));
    }

    [Fact]
    public async Task Setup_Rejects_Invalid_Pin_Format()
    {
        // Arrange
        _currentUserServiceMock.Setup(u => u.UserId).Returns("auth0|123");
        _tenantContextMock.Setup(t => t.TenantId).Returns(Guid.NewGuid());
        var command = new SetupCollectorPinCommand("123"); // Too short

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _mediator.Send(command));
    }

    [Fact]
    public async Task Setup_Saves_Hashed_Pin()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var auth0Id = "auth0|collector";
        var user = await CreateUser(auth0Id);
        
        _dbContext.UserScopeAssignments.Add(new UserScopeAssignment {
            UserId = user.Id,
            Role = "Collector",
            ScopeType = ScopeType.Tenant,
            TargetId = tenantId
        });
        await _dbContext.SaveChangesAsync();

        _currentUserServiceMock.Setup(u => u.UserId).Returns(auth0Id);
        _tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);

        var command = new SetupCollectorPinCommand("1234");

        // Act
        var result = await _mediator.Send(command);

        // Assert
        Assert.True(result.HasPin);
        
        var storedPin = await _dbContext.CollectorPins
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.TenantId == tenantId);
        
        Assert.NotNull(storedPin);
        Assert.NotEqual("1234", storedPin.PinHash);
    }

    [Fact]
    public async Task Verify_Succeeds_With_Correct_Pin()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var auth0Id = "auth0|collector";
        var user = await CreateUser(auth0Id);
        
        _dbContext.UserScopeAssignments.Add(new UserScopeAssignment {
            UserId = user.Id,
            Role = "Collector",
            ScopeType = ScopeType.Tenant,
            TargetId = tenantId
        });
        await _dbContext.SaveChangesAsync();

        _currentUserServiceMock.Setup(u => u.UserId).Returns(auth0Id);
        _tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);

        await _mediator.Send(new SetupCollectorPinCommand("1234"));

        // Act
        var result = await _mediator.Send(new VerifyCollectorPinCommand("1234"));

        // Assert
        Assert.True(result.Verified);
        Assert.True(result.HasPin);
    }

    [Fact]
    public async Task Verify_Fails_With_Wrong_Pin()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var auth0Id = "auth0|collector";
        var user = await CreateUser(auth0Id);
        
        _dbContext.UserScopeAssignments.Add(new UserScopeAssignment {
            UserId = user.Id,
            Role = "Collector",
            ScopeType = ScopeType.Tenant,
            TargetId = tenantId
        });
        await _dbContext.SaveChangesAsync();

        _currentUserServiceMock.Setup(u => u.UserId).Returns(auth0Id);
        _tenantContextMock.Setup(t => t.TenantId).Returns(tenantId);

        await _mediator.Send(new SetupCollectorPinCommand("1234"));

        // Act
        var result = await _mediator.Send(new VerifyCollectorPinCommand("9999"));

        // Assert
        Assert.False(result.Verified);
        Assert.True(result.HasPin);
    }
}
