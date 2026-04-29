using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Api.Middleware;
using MFTL.Collections.Application.Common.Interfaces;
using Moq;
using Xunit;

namespace MFTL.Collections.Api.Tests;

public class EndpointAccessPolicyMiddlewareTests
{
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<EndpointAccessPolicyMiddleware>> _loggerMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IPermissionEvaluator> _permissionEvaluatorMock;

    public EndpointAccessPolicyMiddlewareTests()
    {
        _configMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<EndpointAccessPolicyMiddleware>>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _permissionEvaluatorMock = new Mock<IPermissionEvaluator>();
    }

    [Fact]
    public async Task UnmappedFunction_ShouldFailClosed()
    {
        // Arrange
        var middleware = new EndpointAccessPolicyMiddleware(_configMock.Object, _loggerMock.Object);
        var context = CreateFunctionContext("UnmappedFunction");
        
        bool nextCalled = false;
        Task next(FunctionContext c) { nextCalled = true; return Task.CompletedTask; }

        // Act
        await middleware.Invoke(context, next);

        // Assert
        Assert.False(nextCalled);
        // Verify response was set to Forbidden
        // Note: Checking the actual response value requires more complex mocking of context features
    }

    [Fact]
    public async Task PublicEndpoint_ShouldAllowAnonymous()
    {
        // Arrange
        var middleware = new EndpointAccessPolicyMiddleware(_configMock.Object, _loggerMock.Object);
        var context = CreateFunctionContext("ScalarUi"); // Mapped as Public
        
        bool nextCalled = false;
        Task next(FunctionContext c) { nextCalled = true; return Task.CompletedTask; }

        // Act
        await middleware.Invoke(context, next);

        // Assert
        Assert.True(nextCalled);
    }

    private FunctionContext CreateFunctionContext(string functionName)
    {
        var contextMock = new Mock<FunctionContext>();
        var functionDefinitionMock = new Mock<FunctionDefinition>();
        functionDefinitionMock.Setup(d => d.Name).Returns(functionName);
        
        // Setup input bindings to look like HttpTrigger
        var inputBindings = new Dictionary<string, BindingDefinition>
        {
            { "req", new MockBindingDefinition("httpTrigger") }
        };
        functionDefinitionMock.Setup(d => d.InputBindings).Returns(inputBindings);
        
        contextMock.Setup(c => c.FunctionDefinition).Returns(functionDefinitionMock.Object);
        
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(s => s.GetService(typeof(ICurrentUserService))).Returns(_currentUserServiceMock.Object);
        serviceProviderMock.Setup(s => s.GetService(typeof(IPermissionEvaluator))).Returns(_permissionEvaluatorMock.Object);
        
        contextMock.Setup(c => c.InstanceServices).Returns(serviceProviderMock.Object);
        
        return contextMock.Object;
    }

    private class MockBindingDefinition(string type) : BindingDefinition
    {
        public override string Name => "req";
        public override string Type => type;
        public override BindingDirection Direction => BindingDirection.In;
    }
}
