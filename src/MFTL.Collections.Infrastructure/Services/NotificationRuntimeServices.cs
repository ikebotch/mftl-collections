using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Infrastructure.Persistence;

namespace MFTL.Collections.Infrastructure.Services;

public sealed class TemplateRenderer(ILogger<TemplateRenderer> logger) : ITemplateRenderer
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{\s*(\w+)\s*\}\}", RegexOptions.Compiled);

    public RenderedTemplate Render(string template, object? model)
    {
        var values = BuildValues(model);
        var rendered = PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (values.TryGetValue(key, out var value))
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            logger.LogDebug("Template variable {Variable} missing from payload.", key);
            return match.Value;
        });

        return new RenderedTemplate(rendered);
    }

    private static Dictionary<string, object?> BuildValues(object? model)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (model == null)
        {
            return values;
        }

        switch (model)
        {
            case IDictionary<string, object?> dict:
                foreach (var pair in dict)
                {
                    values[pair.Key] = pair.Value;
                }
                return values;
            case System.Collections.IDictionary legacyDict:
                foreach (System.Collections.DictionaryEntry pair in legacyDict)
                {
                    if (pair.Key is string key)
                    {
                        values[key] = pair.Value;
                    }
                }
                return values;
            case JsonElement element:
                AppendJson(values, element, null);
                return values;
        }

        foreach (var property in model.GetType().GetProperties())
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            values[property.Name] = property.GetValue(model);
        }

        return values;
    }

    private static void AppendJson(Dictionary<string, object?> values, JsonElement element, string? prefix)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            var key = prefix == null ? property.Name : $"{prefix}.{property.Name}";
            values[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => property.Value.ToString()
            };

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                AppendJson(values, property.Value, key);
            }
        }
    }
}

public sealed class NotificationTemplateResolver(CollectionsDbContext dbContext) : INotificationTemplateResolver
{
    public async Task<NotificationTemplate?> ResolveAsync(
        Guid tenantId,
        Guid? branchId,
        string templateKey,
        NotificationChannel channel,
        CancellationToken cancellationToken = default)
    {
        var channelValue = channel.ToString();

        if (branchId.HasValue)
        {
            var branchTemplate = await dbContext.NotificationTemplates
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => t.IsActive && t.TenantId == tenantId && t.BranchId == branchId && t.TemplateKey == templateKey && t.Channel == channelValue)
                .OrderByDescending(t => t.ModifiedAt ?? t.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (branchTemplate != null)
            {
                return branchTemplate;
            }
        }

        var tenantTemplate = await dbContext.NotificationTemplates
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.IsActive && t.TenantId == tenantId && t.BranchId == null && !t.IsSystemDefault && t.TemplateKey == templateKey && t.Channel == channelValue)
            .OrderByDescending(t => t.ModifiedAt ?? t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantTemplate != null)
        {
            return tenantTemplate;
        }

        return await dbContext.NotificationTemplates
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.IsActive && t.IsSystemDefault && t.TemplateKey == templateKey && t.Channel == channelValue)
            .OrderByDescending(t => t.ModifiedAt ?? t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public sealed class GiantSmsService(
    IConfiguration configuration,
    ILogger<GiantSmsService> logger,
    IHttpClientFactory httpClientFactory) : ISmsService
{
    public async Task<NotificationSendResult> SendAsync(string toPhone, string body, CancellationToken cancellationToken = default)
    {
        var username = configuration["Values:GiantSms:Username"] ?? configuration["GiantSms:Username"];
        var password = configuration["Values:GiantSms:Password"] ?? configuration["GiantSms:Password"];
        var senderId = configuration["Values:GiantSms:SenderId"] ?? configuration["GiantSms:SenderId"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(senderId))
        {
            logger.LogWarning("GiantSMS credentials are not configured.");
            return new NotificationSendResult(false, Error: "GiantSMS credentials are not configured.");
        }

        var client = httpClientFactory.CreateClient(nameof(GiantSmsService));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://smsc.giantsms.com/api/v5/sms/send")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = username,
                ["password"] = password,
                ["sender"] = senderId,
                ["to"] = toPhone,
                ["message"] = body
            })
        };

        try
        {
            var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new NotificationSendResult(false, Error: $"GiantSMS HTTP {(int)response.StatusCode}: {content}");
            }

            return new NotificationSendResult(true, ProviderMessageId: content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send SMS through GiantSMS.");
            return new NotificationSendResult(false, Error: ex.Message);
        }
    }
}

public sealed class OutboxService(
    CollectionsDbContext dbContext,
    IHttpContextAccessor httpContextAccessor) : IOutboxService
{
    public async Task<Guid> EnqueueAsync(
        Guid tenantId,
        Guid? branchId,
        Guid aggregateId,
        string aggregateType,
        string eventType,
        object payload,
        string? correlationId = null,
        int priority = 0,
        CancellationToken cancellationToken = default)
    {
        var message = new OutboxMessage
        {
            TenantId = tenantId,
            BranchId = branchId,
            AggregateId = aggregateId,
            AggregateType = aggregateType,
            EventType = eventType,
            CorrelationId = correlationId
                ?? httpContextAccessor.HttpContext?.Request.Headers["x-correlation-id"].ToString()
                ?? Guid.NewGuid().ToString("N"),
            Priority = priority,
            Payload = JsonSerializer.Serialize(payload)
        };

        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        return message.Id;
    }
}
