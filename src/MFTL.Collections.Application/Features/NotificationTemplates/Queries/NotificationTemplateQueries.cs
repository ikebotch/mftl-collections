using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Features.NotificationTemplates.Queries;

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record NotificationTemplateDto(
    Guid Id,
    Guid TenantId,
    Guid? BranchId,
    string TemplateKey,
    string Channel,
    string Name,
    string? Subject,
    string Body,
    string? Description,
    bool IsActive,
    bool IsSystemDefault,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

public record RenderedTemplateDto(string? Subject, string Body);

// ─── List ─────────────────────────────────────────────────────────────────────

[HasPermission("notification-templates.view")]
public record ListNotificationTemplatesQuery(
    string? TemplateKey = null,
    NotificationChannel? Channel = null) : IRequest<List<NotificationTemplateDto>>;

public class ListNotificationTemplatesQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<ListNotificationTemplatesQuery, List<NotificationTemplateDto>>
{
    public async Task<List<NotificationTemplateDto>> Handle(
        ListNotificationTemplatesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.NotificationTemplates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TemplateKey))
            query = query.Where(t => t.TemplateKey == request.TemplateKey);

        if (request.Channel.HasValue)
            query = query.Where(t => t.Channel == request.Channel.Value);

        return await query
            .OrderBy(t => t.TemplateKey).ThenBy(t => t.Channel)
            .Select(t => new NotificationTemplateDto(
                t.Id, t.TenantId, t.BranchId, t.TemplateKey,
                t.Channel.ToString(), t.Name, t.Subject, t.Body,
                t.Description, t.IsActive, t.IsSystemDefault,
                t.CreatedAt, t.ModifiedAt))
            .ToListAsync(cancellationToken);
    }
}

// ─── GetById ──────────────────────────────────────────────────────────────────

[HasPermission("notification-templates.view")]
public record GetNotificationTemplateByIdQuery(Guid Id) : IRequest<NotificationTemplateDto?>;

public class GetNotificationTemplateByIdQueryHandler(IApplicationDbContext dbContext)
    : IRequestHandler<GetNotificationTemplateByIdQuery, NotificationTemplateDto?>
{
    public async Task<NotificationTemplateDto?> Handle(
        GetNotificationTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.NotificationTemplates
            .Where(t => t.Id == request.Id)
            .Select(t => new NotificationTemplateDto(
                t.Id, t.TenantId, t.BranchId, t.TemplateKey,
                t.Channel.ToString(), t.Name, t.Subject, t.Body,
                t.Description, t.IsActive, t.IsSystemDefault,
                t.CreatedAt, t.ModifiedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

// ─── Preview ──────────────────────────────────────────────────────────────────

[HasPermission("notification-templates.view")]
public record PreviewNotificationTemplateQuery(
    Guid Id,
    Dictionary<string, string> Variables) : IRequest<RenderedTemplateDto?>;

public class PreviewNotificationTemplateQueryHandler(INotificationTemplateService templateService)
    : IRequestHandler<PreviewNotificationTemplateQuery, RenderedTemplateDto?>
{
    public async Task<RenderedTemplateDto?> Handle(
        PreviewNotificationTemplateQuery request, CancellationToken cancellationToken)
    {
        var result = await templateService.PreviewAsync(request.Id, request.Variables, cancellationToken);
        if (result == null) return null;
        return new RenderedTemplateDto(result.Value.Subject, result.Value.Body);
    }
}
