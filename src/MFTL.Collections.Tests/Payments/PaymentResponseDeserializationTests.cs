using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
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
    private (PaymentOrchestrator Orchestrator, Guid ContributionId, List<string> RequestBodies) CreateOrchestrator(
        string jsonResponse,
        string cardProvider = "Stripe",
        bool allowStripeCardFallback = false,
        bool mollieEnabled = false)
    {
        var requestBodies = new List<string>();
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Returns<HttpRequestMessage, CancellationToken>(async (request, ct) =>
            {
                requestBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct));
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                };
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var options = Options.Create(new PaymentOptions
        {
            BaseUrl = "http://localhost:5005/api/v1",
            ClientApp = "mftl-collections",
            CardProvider = cardProvider,
            AllowStripeCardFallback = allowStripeCardFallback,
            Internal = new InternalPaymentOptions { SharedSecret = "secret" }
        });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentProviders:Mollie:Enabled"] = mollieEnabled.ToString()
            })
            .Build();

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
            configuration,
            NullLogger<PaymentOrchestrator>.Instance);

        return (orchestrator, contributionId, requestBodies);
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

        var (orchestrator, contributionId, _) = CreateOrchestrator(jsonResponse);

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

        var (orchestrator, contributionId, _) = CreateOrchestrator(jsonResponse);

        // Act
        var result = await orchestrator.InitiatePaymentAsync(contributionId, 100m, "card");

        // Assert
        result.Success.Should().BeTrue();
        result.ProviderReference.Should().Be("ref123");
    }

    [Theory]
    [InlineData("stripe", 1)]
    [InlineData("paystack", 2)]
    [InlineData("moolre", 3)]
    [InlineData("momo", 3)]
    [InlineData("gocardless", 4)]
    [InlineData("bank", 4)]
    [InlineData("bank_debit", 4)]
    [InlineData("direct-debit", 4)]
    [InlineData("direct_debit", 4)]
    [InlineData("mollie", 5)]
    public async Task InitiatePaymentAsync_MapsKnownMethodsToExpectedProvider(string method, int expectedProvider)
    {
        var jsonResponse = @"{
            ""id"": ""56658405-39d9-4546-ab28-208baa1667e1"",
            ""status"": ""Pending"",
            ""provider"": ""Stripe"",
            ""providerReference"": ""ref123"",
            ""checkoutUrl"": ""https://checkout.test"",
            ""externalReference"": ""ext123""
        }";
        var (orchestrator, contributionId, requestBodies) = CreateOrchestrator(jsonResponse);

        var result = await orchestrator.InitiatePaymentAsync(contributionId, 100m, method);

        result.Success.Should().BeTrue();
        requestBodies.Should().ContainSingle();
        using var document = JsonDocument.Parse(requestBodies.Single());
        document.RootElement.GetProperty("provider").GetInt32().Should().Be(expectedProvider);
    }

    [Fact]
    public async Task InitiatePaymentAsync_MapsCardToMollieOnlyWhenEnabledAndConfigured()
    {
        var (orchestrator, contributionId, requestBodies) = CreateOrchestrator(SuccessResponse("Mollie"), cardProvider: "Mollie", mollieEnabled: true);

        var result = await orchestrator.InitiatePaymentAsync(contributionId, 100m, "card");

        result.Success.Should().BeTrue();
        using var document = JsonDocument.Parse(requestBodies.Single());
        document.RootElement.GetProperty("provider").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task InitiatePaymentAsync_RejectsCardWhenMollieConfiguredButDisabled()
    {
        var (orchestrator, contributionId, requestBodies) = CreateOrchestrator("{}", cardProvider: "Mollie", mollieEnabled: false);

        var result = await orchestrator.InitiatePaymentAsync(contributionId, 100m, "card");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Unsupported payment method");
        requestBodies.Should().BeEmpty();
    }

    [Fact]
    public async Task InitiatePaymentAsync_MapsCardToStripeOnlyWhenConfigured()
    {
        var (orchestrator, contributionId, requestBodies) = CreateOrchestrator(SuccessResponse("Stripe"), cardProvider: "Stripe");

        var result = await orchestrator.InitiatePaymentAsync(contributionId, 100m, "card");

        result.Success.Should().BeTrue();
        using var document = JsonDocument.Parse(requestBodies.Single());
        document.RootElement.GetProperty("provider").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task InitiatePaymentAsync_MapsCardToStripeOnlyWhenFallbackIsExplicit()
    {
        var (orchestrator, contributionId, requestBodies) = CreateOrchestrator(SuccessResponse("Stripe"), allowStripeCardFallback: true);

        var result = await orchestrator.InitiatePaymentAsync(contributionId, 100m, "card");

        result.Success.Should().BeTrue();
        using var document = JsonDocument.Parse(requestBodies.Single());
        document.RootElement.GetProperty("provider").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task InitiatePaymentAsync_RejectsCardWithoutExplicitProviderOrFallback()
    {
        var (orchestrator, contributionId, requestBodies) = CreateOrchestrator("{}", cardProvider: "");

        var result = await orchestrator.InitiatePaymentAsync(contributionId, 100m, "card");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Unsupported payment method");
        requestBodies.Should().BeEmpty();
    }

    [Fact]
    public async Task InitiatePaymentAsync_RejectsUnknownMethodWithoutDefaultingToStripe()
    {
        var (orchestrator, contributionId, requestBodies) = CreateOrchestrator("{}");

        var result = await orchestrator.InitiatePaymentAsync(contributionId, 100m, "unknown-method");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Unsupported payment method");
        requestBodies.Should().BeEmpty();
    }

    private static string SuccessResponse(string provider) =>
        $$"""
        {
            "id": "56658405-39d9-4546-ab28-208baa1667e1",
            "status": "Pending",
            "provider": "{{provider}}",
            "providerReference": "ref123",
            "checkoutUrl": "https://checkout.test",
            "externalReference": "ext123"
        }
        """;
}
