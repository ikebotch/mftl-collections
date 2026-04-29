using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Admin.Notifications.Queries;

public record OutboxMessageDto(
    Guid Id,
    string EventType,
    Guid AggregateId,
    string Status,
    int AttemptCount,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset? NextAttemptAt,
    string? CorrelationId,
    int Priority);

[HasPermission("notifications.view")]
public record GetOutboxMessagesQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? Status = null) : IRequest<PagedResponse<OutboxMessageDto>>;

public class GetOutboxMessagesQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetOutboxMessagesQuery, PagedResponse<OutboxMessageDto>>
{
    public async Task<PagedResponse<OutboxMessageDto>> Handle(GetOutboxMessagesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.OutboxMessages.AsQueryable();

        if (!string.IsNullOrEmpty(request.Status))
        {
            if (Enum.TryParse<OutboxMessageStatus>(request.Status, true, out var status))
            {
                query = query.Where(m => m.Status == status);
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new OutboxMessageDto(
                m.Id,
                m.EventType,
                m.AggregateId,
                m.Status.ToString(),
                m.AttemptCount,
                m.LastError,
                m.CreatedAt,
                m.ProcessedAt,
                m.NextAttemptAt,
                m.CorrelationId,
                m.Priority))
            .ToListAsync(cancellationToken);

        return new PagedResponse<OutboxMessageDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
