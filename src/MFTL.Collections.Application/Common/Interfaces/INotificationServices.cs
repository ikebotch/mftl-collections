using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Common.Interfaces;

public interface ISmsService
{
    Task<NotificationSendResult> SendAsync(string toPhone, string body, CancellationToken cancellationToken = default);
}

public interface ITemplateRenderer
{
    RenderedTemplate Render(string template, object? model);
}

public interface INotificationTemplateResolver
{
    Task<NotificationTemplate?> ResolveAsync(
        Guid tenantId,
        Guid? branchId,
        string templateKey,
        NotificationChannel channel,
        CancellationToken cancellationToken = default);
}

public interface IOutboxService
{
    Task<Guid> EnqueueAsync(
        Guid tenantId,
        Guid? branchId,
        Guid aggregateId,
        string aggregateType,
        string eventType,
        object payload,
        string? correlationId = null,
        int priority = 0,
        CancellationToken cancellationToken = default);
}

public interface IOutboxProcessor
{
    Task<int> ProcessMessagesAsync(int batchSize = 20, CancellationToken cancellationToken = default);
}

public sealed record RenderedTemplate(string Value);

public sealed record NotificationSendResult(bool Success, string? ProviderMessageId = null, string? Error = null);
