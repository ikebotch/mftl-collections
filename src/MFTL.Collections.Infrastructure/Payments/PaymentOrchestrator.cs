using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Infrastructure.Configuration;

namespace MFTL.Collections.Infrastructure.Payments;

public sealed class PaymentOrchestrator(
    HttpClient httpClient,
    IOptions<PaymentOptions> options,
    IApplicationDbContext dbContext,
    ILogger<PaymentOrchestrator> logger) : IPaymentOrchestrator
{
    private readonly PaymentOptions _options = options.Value;

    public async Task<PaymentResult> InitiatePaymentAsync(Guid contributionId, decimal amount, string method, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    {
        var contribution = await dbContext.Contributions
            .Include(c => c.Contributor)
            .FirstOrDefaultAsync(c => c.Id == contributionId, cancellationToken);

        if (contribution == null)
        {
            return new PaymentResult(false, null, null, null, null, "Contribution not found.");
        }

        var provider = method.Equals("paystack", StringComparison.OrdinalIgnoreCase) ? 2 : 1; // 1 = Stripe, 2 = Paystack

        var requestMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["contributionId"] = contribution.Id.ToString(),
            ["tenantId"] = contribution.TenantId.ToString()
        };

        if (metadata != null)
        {
            foreach (var kvp in metadata)
            {
                requestMetadata[kvp.Key] = kvp.Value;
            }
        }

        var request = new
        {
            ClientApp = _options.ClientApp,
            ExternalReference = contribution.Reference,
            Provider = provider,
            Amount = amount,
            Currency = contribution.Currency,
            CustomerEmail = contribution.Contributor?.Email,
            CustomerPhone = contribution.Contributor?.PhoneNumber,
            Description = $"Contribution for {contribution.ContributorName}",
            TenantId = contribution.TenantId,
            ContributionId = contribution.Id,
            Metadata = requestMetadata
        };

        try
        {
            if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            {
                logger.LogWarning("Payment service BaseUrl is not configured.");
                return new PaymentResult(false, null, null, null, null, "Payment service integration is not configured.");
            }

            var response = await httpClient.PostAsJsonAsync($"{_options.BaseUrl.TrimEnd('/')}/payments", request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Payment service initiation failed. StatusCode={StatusCode} Body={Body}", (int)response.StatusCode, body);
                return new PaymentResult(false, null, null, null, null, $"Payment service error: {body}");
            }

            var result = JsonSerializer.Deserialize<PaymentServiceResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result == null)
            {
                return new PaymentResult(false, null, null, null, null, "Failed to parse payment service response.");
            }

            return new PaymentResult(true, result.CheckoutUrl, result.ProviderReference);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling payment service at {Url}", _options.BaseUrl);
            return new PaymentResult(false, null, null, null, null, $"Internal error: {ex.Message}");
        }
    }

    private class PaymentServiceResponse
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string ProviderReference { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
        public string ExternalReference { get; set; } = string.Empty;
    }
}
