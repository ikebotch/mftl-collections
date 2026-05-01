using System.Security.Cryptography;
using System.Text;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Infrastructure.Configuration;

namespace MFTL.Collections.Infrastructure.Payments;

public sealed class PaymentOrchestrator(
    HttpClient httpClient,
    IOptions<PaymentOptions> options,
    IApplicationDbContext dbContext,
    IConfiguration configuration,
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

        if (!TryResolveProvider(method, out var provider))
        {
            return new PaymentResult(false, null, null, null, null, $"Unsupported payment method: {method}.");
        }

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

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        logger.LogInformation("[DEBUG] PaymentOrchestrator: Outgoing request to Payment Service. Payload={Payload}", json);

        try
        {
            if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            {
                logger.LogWarning("Payment service BaseUrl is not configured.");
                return new PaymentResult(false, null, null, null, null, "Payment service integration is not configured.");
            }

            logger.LogInformation("Payment service configured BaseUrl={BaseUrl} ClientApp={ClientApp} HasSecret={HasSecret}", 
                _options.BaseUrl, _options.ClientApp, !string.IsNullOrEmpty(_options.SharedSecret));

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signature = ComputeSignature(_options.SharedSecret ?? string.Empty, timestamp, json);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/payments")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Add("X-MFTL-Timestamp", timestamp);
            httpRequest.Headers.Add("X-MFTL-Signature", signature);
            httpRequest.Headers.Add("X-MFTL-Client-App", _options.ClientApp);

            logger.LogInformation("Initiating payment service call: Url={Url} ClientApp={ClientApp} HasSecret={HasSecret}", 
                _options.BaseUrl, _options.ClientApp, !string.IsNullOrEmpty(_options.SharedSecret));

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogInformation("[DEBUG] PaymentOrchestrator: Received response from Payment Service. Status={Status}, Body={Body}", response.StatusCode, body);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Payment service initiation failed. StatusCode={StatusCode} Body={Body}", (int)response.StatusCode, body);
                return new PaymentResult(false, null, null, null, null, $"Payment service error: {response.StatusCode}");
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

    private bool TryResolveProvider(string method, out int provider)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            provider = 0;
            return false;
        }

        provider = method.Trim().ToLowerInvariant() switch
        {
            "stripe" => 1,
            "paystack" => 2,
            "moolre" => 3,
            "momo" => 3,
            "gocardless" => 4,
            "bank" => 4,
            "bank_debit" => 4,
            "direct-debit" => 4,
            "direct_debit" => 4,
            "mollie" => 5,
            "card" => ResolveCardProvider(),
            _ => 0
        };

        return provider != 0;
    }

    private int ResolveCardProvider()
    {
        var cardProvider = (_options.CardProvider ?? string.Empty).Trim();
        var mollieEnabled = configuration.GetValue<bool>("PaymentProviders:Mollie:Enabled");
        if (cardProvider.Equals("Mollie", StringComparison.OrdinalIgnoreCase) && mollieEnabled)
            return 5;

        if (cardProvider.Equals("Stripe", StringComparison.OrdinalIgnoreCase) || _options.AllowStripeCardFallback)
            return 1;

        return 0;
    }

    private static string ComputeSignature(string secret, string timestamp, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private class PaymentServiceResponse
    {
        public Guid Id { get; set; }

        [JsonConverter(typeof(FlexibleStatusConverter))]
        public string Status { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string ProviderReference { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
        public string ExternalReference { get; set; } = string.Empty;
    }

    private class FlexibleStatusConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString() ?? string.Empty;
            }
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt32(out int val))
                {
                    return val switch
                    {
                        0 => "Pending",
                        1 => "Succeeded",
                        2 => "Failed",
                        3 => "Cancelled",
                        4 => "Refunded",
                        _ => val.ToString()
                    };
                }
                return reader.GetDecimal().ToString();
            }
            return string.Empty;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
