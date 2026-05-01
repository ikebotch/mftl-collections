using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Infrastructure.Tenancy;

namespace MFTL.Collections.Api.Functions.Payments;

public class InternalPaymentCallbackFunction(
    IConfiguration configuration,
    IApplicationDbContext dbContext,
    IContributionSettlementService settlementService,
    ITenantContext tenantContext,
    ILogger<InternalPaymentCallbackFunction> logger)
{
    private static readonly TimeSpan MaxDrift = TimeSpan.FromMinutes(5);

    [Function("InternalPaymentCallback")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "internal/payments/callbacks")] HttpRequest req)
    {
        if (tenantContext is TenantContext tc)
        {
            tc.SetSystemContext();
        }

        var timestampHeader = req.Headers["X-MFTL-Timestamp"].FirstOrDefault();
        var signatureHeader = req.Headers["X-MFTL-Signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(timestampHeader) || string.IsNullOrEmpty(signatureHeader))
        {
            return new UnauthorizedObjectResult("Missing security headers.");
        }

        if (!long.TryParse(timestampHeader, out var timestampSeconds))
        {
            return new UnauthorizedObjectResult("Invalid timestamp format.");
        }

        var timestampDate = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        if (Math.Abs((DateTimeOffset.UtcNow - timestampDate).TotalMinutes) > MaxDrift.TotalMinutes)
        {
            return new UnauthorizedObjectResult("Timestamp expired/stale.");
        }

        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var secret = configuration["Values:Payments:Internal:SharedSecret"] ?? configuration["Payments:Internal:SharedSecret"];

        if (string.IsNullOrEmpty(secret))
        {
            logger.LogError("Internal SharedSecret is not configured.");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }

        if (!VerifySignature(secret, timestampHeader, body, signatureHeader))
        {
            return new UnauthorizedObjectResult("Invalid signature.");
        }

        try
        {
            CallbackPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<CallbackPayload>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Malformed JSON in internal callback.");
                return new BadRequestObjectResult("Malformed JSON payload.");
            }

            if (!IsRequiredPayloadPresent(payload))
            {
                return new BadRequestObjectResult("Invalid payload.");
            }

            var existingCallback = await dbContext.ProcessedExternalPaymentCallbacks
                .FirstOrDefaultAsync(x => x.CallbackEventId == payload!.CallbackEventId);

            if (existingCallback != null)
            {
                logger.LogInformation("Callback event {CallbackEventId} was already received with status {Status}.", payload!.CallbackEventId, existingCallback.Status);
                return existingCallback.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase)
                    ? new BadRequestObjectResult("Callback event was previously rejected.")
                    : new OkResult();
            }

            var processedRecord = new ProcessedExternalPaymentCallback
            {
                CallbackEventId = payload!.CallbackEventId,
                PaymentServicePaymentId = payload.PaymentServicePaymentId,
                TenantId = payload.TenantId!.Value,
                ContributionId = payload.ContributionId!.Value,
                Provider = payload.Provider,
                ProviderReference = payload.ProviderReference,
                ProviderTransactionId = payload.ProviderTransactionId,
                ExternalReference = payload.ExternalReference,
                EventType = payload.EventType,
                Amount = payload.Amount,
                Currency = payload.Currency.ToUpperInvariant(),
                OccurredAt = payload.OccurredAt,
                PayloadHash = ComputeSha256(body),
                Status = "Received",
                ProcessedAt = DateTimeOffset.UtcNow
            };

            dbContext.ProcessedExternalPaymentCallbacks.Add(processedRecord);

            var contribution = await dbContext.Contributions
                .IgnoreQueryFilters()
                .Include(c => c.Event)
                .Include(c => c.RecipientFund)
                .Include(c => c.Payment)
                .Include(c => c.Receipt)
                .FirstOrDefaultAsync(c => c.Id == payload.ContributionId.Value);

            if (contribution == null)
            {
                logger.LogWarning("Contribution {ContributionId} not found for callback {CallbackEventId}.", payload.ContributionId, payload.CallbackEventId);
                processedRecord.Status = "Rejected";
                processedRecord.Error = "Contribution not found";
                await dbContext.SaveChangesAsync(default);
                return new BadRequestObjectResult("Contribution not found.");
            }

            var validationError = ValidateContributionBoundary(contribution, payload);
            if (validationError != null)
            {
                logger.LogWarning("Rejected payment callback {CallbackEventId}: {Error}", payload.CallbackEventId, validationError);
                processedRecord.Status = "Rejected";
                processedRecord.Error = validationError;
                await dbContext.SaveChangesAsync(default);
                return new BadRequestObjectResult(validationError);
            }

            var localPayment = await ResolveLocalPaymentAsync(payload, contribution);
            var paymentValidationError = ValidatePaymentBoundary(localPayment, contribution, payload);
            if (paymentValidationError != null)
            {
                logger.LogWarning("Rejected payment callback {CallbackEventId}: {Error}", payload.CallbackEventId, paymentValidationError);
                processedRecord.Status = "Rejected";
                processedRecord.Error = paymentValidationError;
                await dbContext.SaveChangesAsync(default);
                return new BadRequestObjectResult(paymentValidationError);
            }

            processedRecord.Status = "Validated";

            if (payload.EventType.Equals("PaymentSucceeded", StringComparison.OrdinalIgnoreCase))
            {
                if (localPayment != null)
                {
                    localPayment.Status = PaymentStatus.Succeeded;
                    localPayment.ProviderReference = string.IsNullOrWhiteSpace(payload.ProviderReference) ? localPayment.ProviderReference : payload.ProviderReference;
                    localPayment.ProcessedAt = DateTimeOffset.UtcNow;
                    contribution.PaymentId = localPayment.Id;
                    contribution.Payment = localPayment;
                }

                if (contribution.Status != ContributionStatus.Completed)
                {
                    await settlementService.SettleContributionAsync(contribution, localPayment?.Id, null, default);
                    logger.LogInformation("Successfully settled contribution {ContributionId} from internal callback {CallbackEventId}.", contribution.Id, payload.CallbackEventId);
                }

                processedRecord.Status = "Processed";
                await dbContext.SaveChangesAsync(default);
            }
            else if (payload.EventType.Equals("PaymentFailed", StringComparison.OrdinalIgnoreCase))
            {
                if (contribution.Status == ContributionStatus.Completed || localPayment?.Status == PaymentStatus.Succeeded)
                {
                    processedRecord.Status = "Ignored";
                    processedRecord.Error = "Failure callback ignored because contribution/payment is already settled.";
                    await dbContext.SaveChangesAsync(default);
                    return new OkResult();
                }

                if (contribution.Status is ContributionStatus.Pending or ContributionStatus.AwaitingPayment)
                {
                    contribution.Status = ContributionStatus.Failed;
                    logger.LogInformation("Marked contribution {Id} as failed from internal callback.", contribution.Id);
                }

                if (localPayment != null && localPayment.Status is PaymentStatus.Pending or PaymentStatus.Initiated or PaymentStatus.Processing)
                {
                    localPayment.Status = PaymentStatus.Failed;
                    localPayment.ProcessedAt = DateTimeOffset.UtcNow;
                    contribution.PaymentId = localPayment.Id;
                }

                processedRecord.Status = "Processed";
                await dbContext.SaveChangesAsync(default);
            }
            else
            {
                processedRecord.Status = "Rejected";
                processedRecord.Error = $"Unknown event type: {payload.EventType}";
                await dbContext.SaveChangesAsync(default);
                return new BadRequestObjectResult("Unknown event type.");
            }

            return new OkResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing internal payment callback.");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    private static bool VerifySignature(string secret, string timestamp, string payload, string expectedSignature)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        var computedSignature = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(expectedSignature.ToLowerInvariant())
        );
    }

    private async Task<Payment?> ResolveLocalPaymentAsync(CallbackPayload payload, Contribution contribution)
    {
        return await dbContext.Payments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p =>
                p.ContributionId == contribution.Id ||
                (!string.IsNullOrWhiteSpace(payload.ProviderReference) && p.ProviderReference == payload.ProviderReference) ||
                p.Id.ToString() == payload.PaymentServicePaymentId);
    }

    private static bool IsRequiredPayloadPresent(CallbackPayload? payload)
    {
        return payload != null
            && !string.IsNullOrWhiteSpace(payload.CallbackEventId)
            && !string.IsNullOrWhiteSpace(payload.EventType)
            && !string.IsNullOrWhiteSpace(payload.PaymentServicePaymentId)
            && payload.TenantId.HasValue
            && payload.TenantId.Value != Guid.Empty
            && payload.ContributionId.HasValue
            && payload.ContributionId.Value != Guid.Empty
            && !string.IsNullOrWhiteSpace(payload.ExternalReference)
            && !string.IsNullOrWhiteSpace(payload.Currency);
    }

    private static string? ValidateContributionBoundary(Contribution contribution, CallbackPayload payload)
    {
        if (contribution.Id != payload.ContributionId)
            return "Contribution mismatch.";

        if (contribution.TenantId != payload.TenantId)
            return "Tenant mismatch.";

        if (!string.Equals(contribution.Reference, payload.ExternalReference, StringComparison.Ordinal))
            return "External reference mismatch.";

        if (ToMinorUnits(contribution.Amount) != ToMinorUnits(payload.Amount))
            return "Amount mismatch.";

        if (!string.Equals(contribution.Currency, payload.Currency, StringComparison.OrdinalIgnoreCase))
            return "Currency mismatch.";

        if (payload.Amount <= 0 || contribution.Amount <= 0)
            return "Amount must be greater than zero.";

        if (contribution.Event == null || contribution.RecipientFund == null)
            return "Contribution event/fund relationship is incomplete.";

        if (contribution.Event.TenantId != contribution.TenantId || contribution.RecipientFund.TenantId != contribution.TenantId)
            return "Event/fund tenant mismatch.";

        if (contribution.Event.BranchId != contribution.BranchId || contribution.RecipientFund.BranchId != contribution.BranchId)
            return "Event/fund branch mismatch.";

        if (contribution.RecipientFund.EventId != contribution.EventId)
            return "Fund event mismatch.";

        return null;
    }

    private static string? ValidatePaymentBoundary(Payment? payment, Contribution contribution, CallbackPayload payload)
    {
        if (payment == null)
            return null;

        if (payment.TenantId != payload.TenantId || payment.TenantId != contribution.TenantId)
            return "Payment tenant mismatch.";

        if (payment.ContributionId != contribution.Id)
            return "Payment contribution mismatch.";

        if (ToMinorUnits(payment.Amount) != ToMinorUnits(payload.Amount))
            return "Payment amount mismatch.";

        if (!string.Equals(payment.Currency, payload.Currency, StringComparison.OrdinalIgnoreCase))
            return "Payment currency mismatch.";

        return null;
    }

    private static long ToMinorUnits(decimal amount) =>
        decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

    private static string ComputeSha256(string payload)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private class CallbackPayload
    {
        public string CallbackEventId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string PaymentServicePaymentId { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
        public Guid? ContributionId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string ProviderReference { get; set; } = string.Empty;
        public string ProviderTransactionId { get; set; } = string.Empty;
        public string ClientApp { get; set; } = string.Empty;
        public string ExternalReference { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("status")]
        [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleStatusConverter))]
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset? OccurredAt { get; set; }
    }

    private class FlexibleStatusConverter : System.Text.Json.Serialization.JsonConverter<string>
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
                    // Map common enum values if needed, or just return as string
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
