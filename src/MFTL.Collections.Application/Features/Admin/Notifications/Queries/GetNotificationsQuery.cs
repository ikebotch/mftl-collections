using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Admin.Notifications.Queries;

public record NotificationDto(
    Guid Id,
    Guid? OutboxMessageId,
    string? RecipientUserId,
    string? RecipientEmail,
    string? RecipientPhone,
    string Channel,
    string TemplateKey,
    string? Subject,
    string Body,
    string Status,
    string? ProviderMessageId,
    string? Error,
    DateTimeOffset? SentAt,
    DateTimeOffset CreatedAt,
    string? CorrelationId);

[HasPermission("notifications.view")]
public record GetNotificationsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Status = null,
    string? Channel = null) : IRequest<PagedResponse<NotificationDto>>;

public class GetNotificationsQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetNotificationsQuery, PagedResponse<NotificationDto>>
{
    public async Task<PagedResponse<NotificationDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Notifications.AsQueryable();

        if (!string.IsNullOrEmpty(request.Status))
        {
            if (Enum.TryParse<NotificationStatus>(request.Status, true, out var status))
            {
                query = query.Where(n => n.Status == status);
            }
        }

        if (!string.IsNullOrEmpty(request.Channel))
        {
            if (Enum.TryParse<NotificationChannel>(request.Channel, true, out var channel))
            {
                query = query.Where(n => n.Channel == channel);
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new NotificationDto(
                n.Id,
                n.OutboxMessageId,
                n.RecipientUserId,
                n.RecipientEmail,
                n.RecipientPhone,
                n.Channel.ToString(),
                n.TemplateKey,
                n.Subject,
                n.Body,
                n.Status.ToString(),
                n.ProviderMessageId,
                n.Error,
                n.SentAt,
                n.CreatedAt,
                n.CorrelationId))
            .ToListAsync(cancellationToken);

        return new PagedResponse<NotificationDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
