using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Common.Interfaces;

/// <summary>
/// Resolves and renders notification templates from the database,
/// with branch → tenant → system-default fallback.
/// </summary>
public interface INotificationTemplateService
{
    /// <summary>
    /// Resolve the best matching active template for the given key/channel/context.
    /// Priority: branch-specific → tenant-level → system default.
    /// Returns null if no active template is found.
    /// </summary>
    Task<NotificationTemplate?> GetTemplateAsync(
        string templateKey,
        NotificationChannel channel,
        Guid tenantId,
        Guid? branchId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Render subject and body for the given template key, channel, and variables.
    /// Returns null if no active template is found.
    /// </summary>
    Task<(string? Subject, string Body)?> RenderAsync(
        string templateKey,
        NotificationChannel channel,
        Guid tenantId,
        Guid? branchId,
        object variables,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Preview a specific template (by ID) with given variables.
    /// Used by the admin preview endpoint.
    /// </summary>
    Task<(string? Subject, string Body)?> PreviewAsync(
        Guid templateId,
        object variables,
        CancellationToken cancellationToken = default);
}
