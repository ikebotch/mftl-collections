using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Configuration;
using MFTL.Collections.Infrastructure.Payments;
using Moq;
using Moq.Protected;
using FluentAssertions;
using MockQueryable.Moq;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Tests.Payments;

public class PaymentResponseDeserializationTests
{
    private (PaymentOrchestrator Orchestrator, Guid ContributionId) CreateOrchestrator(string jsonResponse)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var options = Options.Create(new PaymentOptions
        {
            BaseUrl = "http://localhost:5005/api/v1",
            ClientApp = "mftl-collections",
            Internal = new InternalPaymentOptions { SharedSecret = "secret" }
        });

        var dbContextMock = new Mock<IApplicationDbContext>();
        
        var contributionId = Guid.NewGuid();
        var contributionsList = new List<MFTL.Collections.Domain.Entities.Contribution>
        {
            new MFTL.Collections.Domain.Entities.Contribution
            {
                Id = contributionId,
                Reference = "REF-123",
                Currency = "GHS",
                ContributorName = "Isaac Test",
                TenantId = Guid.NewGuid()
            }
        };

        var contributions = contributionsList.BuildMockDbSet<MFTL.Collections.Domain.Entities.Contribution>();

        dbContextMock.Setup(x => x.Contributions).Returns(contributions.Object);
        
        var orchestrator = new PaymentOrchestrator(
            httpClient,
            options,
            dbContextMock.Object,
            NullLogger<PaymentOrchestrator>.Instance);

        return (orchestrator, contributionId);
    }

    [Fact]
    public async Task InitiatePaymentAsync_CanDeserializeStringStatus()
    {
        // Arrange
        var jsonResponse = @"{
            ""id"": ""56658405-39d9-4546-ab28-208baa1667e1"",
            ""status"": ""Pending"",
            ""provider"": ""Stripe"",
            ""providerReference"": ""ref123"",
            ""checkoutUrl"": ""https://checkout.test"",
            ""externalReference"": ""ext123""
        }";

        var (orchestrator, contributionId) = CreateOrchestrator(jsonResponse);

        // Act
        var result = await orchestrator.InitiatePaymentAsync(contributionId, 100m, "card");

        // Assert
        result.Success.Should().BeTrue();
        result.ProviderReference.Should().Be("ref123");
        result.RedirectUrl.Should().Be("https://checkout.test");
    }

    [Fact]
    public async Task InitiatePaymentAsync_CanDeserializeNumericStatus()
    {
        // Arrange
        var jsonResponse = @"{
            ""id"": ""56658405-39d9-4546-ab28-208baa1667e1"",
            ""status"": 1,
            ""provider"": ""Stripe"",
            ""providerReference"": ""ref123"",
            ""checkoutUrl"": ""https://checkout.test"",
            ""externalReference"": ""ext123""
        }";

        var (orchestrator, contributionId) = CreateOrchestrator(jsonResponse);

        // Act
        var result = await orchestrator.InitiatePaymentAsync(contributionId, 100m, "card");

        // Assert
        result.Success.Should().BeTrue();
        result.ProviderReference.Should().Be("ref123");
    }
}
