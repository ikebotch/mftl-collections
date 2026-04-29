using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Application.Features.Notifications.Queries.ListNotificationTemplates;

public record ListNotificationTemplatesQuery : IRequest<IEnumerable<NotificationTemplateDto>>
{
    public string? TemplateKey { get; init; }
    public string? Channel { get; init; }
}

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

public class ListNotificationTemplatesQueryHandler(IApplicationDbContext context) : IRequestHandler<ListNotificationTemplatesQuery, IEnumerable<NotificationTemplateDto>>
{
    public async Task<IEnumerable<NotificationTemplateDto>> Handle(ListNotificationTemplatesQuery request, CancellationToken cancellationToken)
    {
        var query = context.NotificationTemplates.AsNoTracking();

        if (!string.IsNullOrEmpty(request.TemplateKey))
        {
            query = query.Where(x => x.TemplateKey == request.TemplateKey);
        }

        if (!string.IsNullOrEmpty(request.Channel))
        {
            query = query.Where(x => x.Channel == request.Channel);
        }

        var results = await query
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return results.Select(x => new NotificationTemplateDto(
            x.Id,
            x.TenantId,
            x.BranchId,
            x.TemplateKey,
            x.Channel,
            x.Name,
            x.Subject,
            x.Body,
            x.Description,
            x.IsActive,
            x.IsSystemDefault,
            x.CreatedAt,
            x.ModifiedAt));
    }
}
