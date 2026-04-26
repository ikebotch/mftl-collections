using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Users.Queries.GetUserAuditLogs;

public record GetUserAuditLogsQuery(Guid UserId) : IRequest<IEnumerable<AuditLogDto>>;

public record AuditLogDto(
    Guid Id,
    string Action,
    string Details,
    string PerformedBy,
    DateTimeOffset CreatedAt);

public class GetUserAuditLogsHandler(IApplicationDbContext dbContext) : IRequestHandler<GetUserAuditLogsQuery, IEnumerable<AuditLogDto>>
{
    public async Task<IEnumerable<AuditLogDto>> Handle(GetUserAuditLogsQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.AuditLogs
            .Where(a => a.EntityName == "User" && a.EntityId == request.UserId.ToString())
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AuditLogDto(
                a.Id,
                a.Action,
                a.Details,
                a.PerformedBy,
                a.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
