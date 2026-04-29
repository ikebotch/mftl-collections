using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.SmsTemplates.Queries.GetSmsTemplateById;

[HasPermission("sms_templates.read")]
public record GetSmsTemplateByIdQuery(Guid Id) : IRequest<SmsTemplateDto?>;

public class GetSmsTemplateByIdQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetSmsTemplateByIdQuery, SmsTemplateDto?>
{
    public async Task<SmsTemplateDto?> Handle(GetSmsTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.SmsTemplates
            .AsNoTracking()
            .Where(t => t.Id == request.Id)
            .Select(t => new SmsTemplateDto(t.Id, t.Name, t.Content))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
