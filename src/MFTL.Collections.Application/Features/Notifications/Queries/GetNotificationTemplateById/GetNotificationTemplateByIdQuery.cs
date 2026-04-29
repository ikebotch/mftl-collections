using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Notifications.Queries.ListNotificationTemplates;

namespace MFTL.Collections.Application.Features.Notifications.Queries.GetNotificationTemplateById;

public record GetNotificationTemplateByIdQuery(Guid Id) : IRequest<NotificationTemplateDto?>;

public class GetNotificationTemplateByIdQueryHandler(IApplicationDbContext context) : IRequestHandler<GetNotificationTemplateByIdQuery, NotificationTemplateDto?>
{
    public async Task<NotificationTemplateDto?> Handle(GetNotificationTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var x = await context.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (x == null) return null;

        return new NotificationTemplateDto(
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
            x.ModifiedAt);
    }
}
