using MediatR;
using FluentValidation;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.SmsTemplates.Commands.CreateSmsTemplate;

[HasPermission("sms_templates.create")]
public record CreateSmsTemplateCommand(string Name, string Content) : IRequest<Guid>;

public class CreateSmsTemplateCommandValidator : AbstractValidator<CreateSmsTemplateCommand>
{
    public CreateSmsTemplateCommandValidator()
    {
        RuleFor(v => v.Name).NotEmpty().MaximumLength(100);
        RuleFor(v => v.Content).NotEmpty().MaximumLength(500);
    }
}

public class CreateSmsTemplateCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<CreateSmsTemplateCommand, Guid>
{
    public async Task<Guid> Handle(CreateSmsTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = new SmsTemplate
        {
            Name = request.Name,
            Content = request.Content
        };

        dbContext.SmsTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);

        return template.Id;
    }
}
