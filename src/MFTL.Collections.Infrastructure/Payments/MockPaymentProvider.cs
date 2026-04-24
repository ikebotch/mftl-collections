using MFTL.Collections.Domain.Enums;
using System.Text.Json;

namespace MFTL.Collections.Infrastructure.Payments;

public class MockPaymentProvider : IPaymentProvider
{
    public string ProviderName => "Mock";

    public bool VerifySignature(string payload, string signature, string secret)
    {
        return true;
    }

    public (Guid ContributionId, string ProviderReference, PaymentStatus Status) ParseWebhook(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        
        var contributionId = root.TryGetProperty("contributionId", out var cIdProp) ? cIdProp.GetGuid() : Guid.Empty;
        var providerRef = root.TryGetProperty("providerReference", out var pRefProp) ? pRefProp.GetString() ?? "" : "";
        var statusStr = root.TryGetProperty("status", out var sProp) ? sProp.GetString() : "pending";
        
        var status = statusStr?.ToLowerInvariant() switch
        {
            "success" or "succeeded" or "completed" => PaymentStatus.Succeeded,
            "failed" or "declined" or "error" => PaymentStatus.Failed,
            "pending" or "processing" => PaymentStatus.Pending,
            _ => PaymentStatus.Pending
        };

        return (contributionId, providerRef, status);
    }
}
