using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.SmsTemplates.Commands.UpdateSmsTemplate;

[HasPermission("sms_templates.update")]
public record UpdateSmsTemplateCommand(Guid Id, string Name, string Content) : IRequest<bool>;

public class UpdateSmsTemplateCommandValidator : AbstractValidator<UpdateSmsTemplateCommand>
{
    public UpdateSmsTemplateCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(100);
        RuleFor(v => v.Content).NotEmpty().MaximumLength(500);
    }
}

public class UpdateSmsTemplateCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<UpdateSmsTemplateCommand, bool>
{
    public async Task<bool> Handle(UpdateSmsTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await dbContext.SmsTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (template == null) return false;

        template.Name = request.Name;
        template.Content = request.Content;

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
