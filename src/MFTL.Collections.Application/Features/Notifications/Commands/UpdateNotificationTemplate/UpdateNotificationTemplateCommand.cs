using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Application.Features.Notifications.Commands.UpdateNotificationTemplate;

public record UpdateNotificationTemplateCommand : IRequest<bool>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Subject { get; init; }
    public string Body { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
}

public class UpdateNotificationTemplateCommandHandler(IApplicationDbContext context) : IRequestHandler<UpdateNotificationTemplateCommand, bool>
{
    public async Task<bool> Handle(UpdateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.NotificationTemplates
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity == null) return false;

        entity.Name = request.Name;
        entity.Subject = request.Subject;
        entity.Body = request.Body;
        entity.Description = request.Description;
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
