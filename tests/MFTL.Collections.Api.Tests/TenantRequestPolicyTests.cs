using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.AspNetCore.Http;
using MFTL.Collections.Api.Middleware;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Configuration;

namespace MFTL.Collections.Api.Tests;

public class TenantRequestPolicyTests
{
    [Fact]
    public void MissingTenantHeader_ReturnsClearError()
    {
        var headers = new HeaderDictionary();
        var options = new TenantResolutionOptions { HeaderName = "X-Tenant-Id" };

        var result = TenantRequestPolicy.Evaluate(
            "ListEvents",
            headers,
            new TenantResolutionResult(null, null, false),
            options);

        result.RequiresTenant.Should().BeTrue();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Tenant header is required.");
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("X-Tenant-Id");
    }

    [Fact]
    public void PlatformRoutes_DoNotRequireTenantHeader()
    {
        var headers = new HeaderDictionary();
        var options = new TenantResolutionOptions { HeaderName = "X-Tenant-Id" };

        var result = TenantRequestPolicy.Evaluate(
            "PaymentWebhook",
            headers,
            new TenantResolutionResult(null, null, false),
            options);

        result.RequiresTenant.Should().BeFalse();
        result.Success.Should().BeTrue();
        result.TenantId.Should().BeNull();
    }
}
