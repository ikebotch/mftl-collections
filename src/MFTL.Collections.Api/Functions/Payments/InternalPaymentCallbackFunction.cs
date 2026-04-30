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
            // Parse payload
            var payload = JsonSerializer.Deserialize<CallbackPayload>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (payload == null || string.IsNullOrEmpty(payload.ExternalReference) || string.IsNullOrEmpty(payload.EventType) || string.IsNullOrEmpty(payload.PaymentId))
            {
                return new BadRequestObjectResult("Invalid payload.");
            }

            // 1. Check Idempotency (has this PaymentServicePaymentId been processed?)
            var existingCallback = await dbContext.ProcessedExternalPaymentCallbacks
                .FirstOrDefaultAsync(x => x.PaymentServicePaymentId == payload.PaymentId);
                
            if (existingCallback != null)
            {
                logger.LogInformation("Callback for PaymentId {PaymentId} was already processed. Idempotent return.", payload.PaymentId);
                return new OkResult();
            }

            var processedRecord = new ProcessedExternalPaymentCallback
            {
                PaymentServicePaymentId = payload.PaymentId,
                Provider = payload.Provider,
                ProviderReference = payload.ProviderReference,
                ProviderTransactionId = payload.ProviderTransactionId,
                ExternalReference = payload.ExternalReference,
                PayloadHash = signatureHeader.ToLowerInvariant(), // Using the signature as the hash
                Status = "Pending"
            };

            dbContext.ProcessedExternalPaymentCallbacks.Add(processedRecord);
            await dbContext.SaveChangesAsync(default);

            // We must use IgnoreQueryFilters since this is an anonymous internal system call,
            // and no TenantContext is set in the middleware.
            var contribution = await dbContext.Contributions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Reference == payload.ExternalReference);

            if (contribution == null)
            {
                logger.LogWarning("Contribution with reference {Ref} not found.", payload.ExternalReference);
                processedRecord.Status = "Failed";
                processedRecord.Error = "Contribution not found";
                await dbContext.SaveChangesAsync(default);
                return new OkResult(); // Acknowledge to stop retries if it's genuinely missing
            }

            // 2. Validate Amount and Currency
            if (Math.Abs(contribution.Amount - payload.Amount) > 0.01m)
            {
                logger.LogWarning("Amount mismatch for {Ref}: Expected {Exp}, Got {Got}.", payload.ExternalReference, contribution.Amount, payload.Amount);
                processedRecord.Status = "Rejected";
                processedRecord.Error = $"Amount mismatch: Expected {contribution.Amount}, Got {payload.Amount}";
                await dbContext.SaveChangesAsync(default);
                return new BadRequestObjectResult("Amount mismatch.");
            }
            
            if (!contribution.Currency.Equals(payload.Currency, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Currency mismatch for {Ref}: Expected {Exp}, Got {Got}.", payload.ExternalReference, contribution.Currency, payload.Currency);
                processedRecord.Status = "Rejected";
                processedRecord.Error = $"Currency mismatch: Expected {contribution.Currency}, Got {payload.Currency}";
                await dbContext.SaveChangesAsync(default);
                return new BadRequestObjectResult("Currency mismatch.");
            }

            // 3. Process Event
            if (payload.EventType.Equals("PaymentSucceeded", StringComparison.OrdinalIgnoreCase))
            {
                if (contribution.Status != ContributionStatus.Completed)
                {
                    // Try to find the local payment record ID if it exists
                    var localPaymentId = await dbContext.Payments
                        .IgnoreQueryFilters()
                        .Where(p => p.ProviderReference == payload.ProviderReference || p.Id.ToString() == payload.PaymentId)
                        .Select(p => (Guid?)p.Id)
                        .FirstOrDefaultAsync(default);

                    // Call settlement service directly to settle the contribution properly
                    await settlementService.SettleContributionAsync(contribution.Id, localPaymentId, default);
                    logger.LogInformation("Successfully settled contribution {Id} from internal callback (LocalPaymentId: {LocalPaymentId}).", contribution.Id, localPaymentId);
                }

                processedRecord.Status = "Processed";
                await dbContext.SaveChangesAsync(default);
            }
            else if (payload.EventType.Equals("PaymentFailed", StringComparison.OrdinalIgnoreCase))
            {
                if (contribution.Status == ContributionStatus.Pending || contribution.Status == ContributionStatus.AwaitingPayment)
                {
                    contribution.Status = ContributionStatus.Failed;
                    logger.LogInformation("Marked contribution {Id} as failed from internal callback.", contribution.Id);
                }
                
                processedRecord.Status = "Processed";
                await dbContext.SaveChangesAsync(default);
            }
            else
            {
                processedRecord.Status = "Ignored";
                processedRecord.Error = $"Unknown event type: {payload.EventType}";
                await dbContext.SaveChangesAsync(default);
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

    private class CallbackPayload
    {
        public string EventType { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string ProviderReference { get; set; } = string.Empty;
        public string ProviderTransactionId { get; set; } = string.Empty;
        public string ClientApp { get; set; } = string.Empty;
        public string ExternalReference { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
