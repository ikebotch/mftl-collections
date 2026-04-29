using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.SmsTemplates.Queries.ListSmsTemplates;

[HasPermission("sms_templates.read")]
public record ListSmsTemplatesQuery() : IRequest<List<SmsTemplateDto>>;

public class ListSmsTemplatesQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListSmsTemplatesQuery, List<SmsTemplateDto>>
{
    public async Task<List<SmsTemplateDto>> Handle(ListSmsTemplatesQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.SmsTemplates
            .AsNoTracking()
            .Select(t => new SmsTemplateDto(t.Id, t.Name, t.Content))
            .ToListAsync(cancellationToken);
    }
}
