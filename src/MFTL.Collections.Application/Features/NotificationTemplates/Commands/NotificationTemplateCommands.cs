using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Application.Features.NotificationTemplates.Commands;

// ─── Create ───────────────────────────────────────────────────────────────────

[HasPermission("notification-templates.manage")]
public record CreateNotificationTemplateCommand(
    string TemplateKey,
    NotificationChannel Channel,
    string Name,
    string? Subject,
    string Body,
    string? Description,
    bool IsActive = true) : IRequest<Guid>;

public class CreateNotificationTemplateCommandValidator : AbstractValidator<CreateNotificationTemplateCommand>
{
    public CreateNotificationTemplateCommandValidator()
    {
        RuleFor(v => v.TemplateKey).NotEmpty().MaximumLength(100);
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Body).NotEmpty();
        RuleFor(v => v.Subject).MaximumLength(500);
    }
}

public class CreateNotificationTemplateCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<CreateNotificationTemplateCommand, Guid>
{
    public async Task<Guid> Handle(CreateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = new NotificationTemplate
        {
            TemplateKey = request.TemplateKey,
            Channel = request.Channel,
            Name = request.Name,
            Subject = request.Subject,
            Body = request.Body,
            Description = request.Description,
            IsActive = request.IsActive,
            IsSystemDefault = false
        };

        dbContext.NotificationTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);
        return template.Id;
    }
}

// ─── Update ───────────────────────────────────────────────────────────────────

[HasPermission("notification-templates.manage")]
public record UpdateNotificationTemplateCommand(
    Guid Id,
    string Name,
    string? Subject,
    string Body,
    string? Description,
    bool IsActive) : IRequest<bool>;

public class UpdateNotificationTemplateCommandValidator : AbstractValidator<UpdateNotificationTemplateCommand>
{
    public UpdateNotificationTemplateCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Body).NotEmpty();
        RuleFor(v => v.Subject).MaximumLength(500);
    }
}

public class UpdateNotificationTemplateCommandHandler(IApplicationDbContext dbContext)
    : IRequestHandler<UpdateNotificationTemplateCommand, bool>
{
    public async Task<bool> Handle(UpdateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await dbContext.NotificationTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (template == null) return false;
        if (template.IsSystemDefault) throw new InvalidOperationException("System default templates cannot be edited via API. Create a tenant-level override.");

        template.Name = request.Name;
        template.Subject = request.Subject;
        template.Body = request.Body;
        template.Description = request.Description;
        template.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
