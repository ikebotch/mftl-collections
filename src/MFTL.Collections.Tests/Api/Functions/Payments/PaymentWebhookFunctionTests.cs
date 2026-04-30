using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using MFTL.Collections.Api.Functions.Webhooks;
using MFTL.Collections.Infrastructure.Payments;
using Xunit;

namespace MFTL.Collections.Tests.Api.Functions.Payments;

public sealed class PaymentWebhookFunctionTests
{
    [Fact]
    public async Task Run_LegacyProviderWebhookDisabledByDefault_ReturnsGoneAndDoesNotProcess()
    {
        var processor = new Mock<IPaymentWebhookProcessor>(MockBehavior.Strict);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var function = new PaymentWebhookFunction(processor.Object, Array.Empty<IPaymentProvider>(), configuration);

        var result = await function.Run(new DefaultHttpContext().Request, "stripe");

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status410Gone, status.StatusCode);
        processor.VerifyNoOtherCalls();
    }
}
