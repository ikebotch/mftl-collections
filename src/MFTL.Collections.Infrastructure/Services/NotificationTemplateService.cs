using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Infrastructure.Services;

/// <summary>
/// Resolves notification templates from the database using a
/// branch → tenant → system-default priority chain.
/// Rendering is delegated to ISmsTemplateService (the existing {{}} renderer).
/// </summary>
public sealed class NotificationTemplateService(
    IApplicationDbContext dbContext,
    ISmsTemplateService renderer,
    ILogger<NotificationTemplateService> logger) : INotificationTemplateService
{
    public async Task<NotificationTemplate?> GetTemplateAsync(
        string templateKey,
        NotificationChannel channel,
        Guid tenantId,
        Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        // Must bypass the global query filter because system defaults have TenantId = Guid.Empty,
        // which the tenant-scoped filter would exclude.
        // We apply tenant/system/branch discrimination manually below.
        var candidates = await dbContext.NotificationTemplates
            .IgnoreQueryFilters()
            .Where(t => t.TemplateKey == templateKey
                     && t.Channel == channel
                     && t.IsActive
                     && (t.TenantId == tenantId || t.IsSystemDefault))
            .ToListAsync(cancellationToken);

        // 1. Branch-specific template (tenant-owned, branch-scoped)
        if (branchId.HasValue)
        {
            var branchTemplate = candidates
                .FirstOrDefault(t => t.TenantId == tenantId && t.BranchId == branchId);
            if (branchTemplate != null) return branchTemplate;
        }

        // 2. Tenant-level template (tenant-owned, no branch)
        var tenantTemplate = candidates
            .FirstOrDefault(t => t.TenantId == tenantId && t.BranchId == null);
        if (tenantTemplate != null) return tenantTemplate;

        // 3. System default (IsSystemDefault = true, TenantId = Guid.Empty)
        var systemDefault = candidates.FirstOrDefault(t => t.IsSystemDefault);
        if (systemDefault != null) return systemDefault;

        logger.LogWarning("No active notification template found for key={Key} channel={Channel}", templateKey, channel);
        return null;
    }

    public async Task<(string? Subject, string Body)?> RenderAsync(
        string templateKey,
        NotificationChannel channel,
        Guid tenantId,
        Guid? branchId,
        object variables,
        CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateAsync(templateKey, channel, tenantId, branchId, cancellationToken);
        if (template == null) return null;

        var body = renderer.Render(template.Body, variables);
        var subject = template.Subject != null ? renderer.Render(template.Subject, variables) : null;
        return (subject, body);
    }

    public async Task<(string? Subject, string Body)?> PreviewAsync(
        Guid templateId,
        object variables,
        CancellationToken cancellationToken = default)
    {
        // Preview must bypass tenant/branch query filters, so use IgnoreQueryFilters
        var template = await dbContext.NotificationTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);

        if (template == null) return null;

        var body = renderer.Render(template.Body, variables);
        var subject = template.Subject != null ? renderer.Render(template.Subject, variables) : null;
        return (subject, body);
    }
}
