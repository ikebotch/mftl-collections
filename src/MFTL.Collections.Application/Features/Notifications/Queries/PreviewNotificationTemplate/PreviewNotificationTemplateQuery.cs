using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Application.Features.Notifications.Queries.PreviewNotificationTemplate;

public record PreviewNotificationTemplateQuery : IRequest<RenderedTemplateDto?>
{
    public Guid Id { get; init; }
    public Dictionary<string, string> Variables { get; init; } = new();
}

public record RenderedTemplateDto(string? Subject, string Body);

public class PreviewNotificationTemplateQueryHandler(IApplicationDbContext context) : IRequestHandler<PreviewNotificationTemplateQuery, RenderedTemplateDto?>
{
    public async Task<RenderedTemplateDto?> Handle(PreviewNotificationTemplateQuery request, CancellationToken cancellationToken)
    {
        var template = await context.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (template == null) return null;

        var subject = template.Subject;
        var body = template.Body;

        foreach (var variable in request.Variables)
        {
            var placeholder = $"{{{{{variable.Key}}}}}";
            subject = subject?.Replace(placeholder, variable.Value);
            body = body.Replace(placeholder, variable.Value);
        }

        return new RenderedTemplateDto(subject, body);
    }
}
