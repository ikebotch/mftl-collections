using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Features.Notifications.Commands.CreateNotificationTemplate;

public record CreateNotificationTemplateCommand : IRequest<Guid>
{
    public string TemplateKey { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Subject { get; init; }
    public string Body { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
    public Guid? BranchId { get; init; }
}

public class CreateNotificationTemplateCommandHandler(IApplicationDbContext context) : IRequestHandler<CreateNotificationTemplateCommand, Guid>
{
    public async Task<Guid> Handle(CreateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var entity = new NotificationTemplate
        {
            TemplateKey = request.TemplateKey,
            Channel = request.Channel,
            Name = request.Name,
            Subject = request.Subject,
            Body = request.Body,
            Description = request.Description,
            IsActive = request.IsActive,
            BranchId = request.BranchId
        };

        context.NotificationTemplates.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
