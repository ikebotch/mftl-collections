using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Infrastructure.Payments;

public sealed record ParsedWebhookResult(string EventId, Guid ContributionId, string ProviderReference, PaymentStatus Status, string? FailureReason = null);

public interface IPaymentProvider
{
    string ProviderName { get; }
    bool VerifySignature(string payload, string signature, string secret);
    ParsedWebhookResult ParseWebhook(string payload);
}

public sealed class StripePaymentProvider : IPaymentProvider
{
    public string ProviderName => "Stripe";

    public bool VerifySignature(string payload, string signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var expected = ComputeHexHmac(secret, payload);
        return signature.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }

    public ParsedWebhookResult ParseWebhook(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var eventId = root.GetProperty("id").GetString() ?? throw new InvalidOperationException("Missing Stripe event ID.");
        var type = root.GetProperty("type").GetString() ?? string.Empty;
        var data = root.GetProperty("data").GetProperty("object");
        var contributionId = Guid.Parse(data.GetProperty("metadata").GetProperty("contributionId").GetString() ?? throw new InvalidOperationException("Missing contributionId metadata."));
        var providerReference = data.GetProperty("id").GetString() ?? throw new InvalidOperationException("Missing Stripe payment id.");

        return new ParsedWebhookResult(
            eventId,
            contributionId,
            providerReference,
            type switch
            {
                "checkout.session.completed" => PaymentStatus.Succeeded,
                "payment_intent.succeeded" => PaymentStatus.Succeeded,
                "payment_intent.payment_failed" => PaymentStatus.Failed,
                "charge.failed" => PaymentStatus.Failed,
                _ => PaymentStatus.Processing
            },
            data.TryGetProperty("last_payment_error", out var error)
                ? error.ToString()
                : null);
    }

    private static string ComputeHexHmac(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}

public sealed class PaystackPaymentProvider : IPaymentProvider
{
    public string ProviderName => "Paystack";

    public bool VerifySignature(string payload, string signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return string.Equals(signature, expected, StringComparison.OrdinalIgnoreCase);
    }

    public ParsedWebhookResult ParseWebhook(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        // Paystack doesn't always have a top-level event ID that is stable across retries? 
        // Actually, we can use a hash or just the combination of event name + reference if needed.
        // But let's try to find a unique one. If not, reference + event name is a good proxy.
        var eventName = root.GetProperty("event").GetString() ?? string.Empty;
        var data = root.GetProperty("data");
        var reference = data.GetProperty("reference").GetString() ?? string.Empty;
        var eventId = $"{eventName}_{reference}";
        
        var contributionId = Guid.Parse(data.GetProperty("metadata").GetProperty("contributionId").GetString() ?? throw new InvalidOperationException("Missing contributionId metadata."));
        var providerReference = reference;

        return new ParsedWebhookResult(
            eventId,
            contributionId,
            providerReference,
            eventName switch
            {
                "charge.success" => PaymentStatus.Succeeded,
                "charge.failed" => PaymentStatus.Failed,
                _ => PaymentStatus.Processing
            },
            data.TryGetProperty("gateway_response", out var reason) ? reason.GetString() : null);
    }
}
