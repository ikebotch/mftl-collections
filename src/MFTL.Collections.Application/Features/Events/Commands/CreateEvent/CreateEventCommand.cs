using MFTL.Collections.Application.Common.Interfaces;
using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Common;
using MFTL.Collections.Contracts.Requests;
using Mapster;

namespace MFTL.Collections.Application.Features.Events.Commands.CreateEvent;

public record CreateEventCommand(string Title, string Description, DateTimeOffset? EventDate, string? Slug = null) : IRequest<EventDto>;

public class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Slug).MaximumLength(100).Matches(@"^[a-z0-9-]*$").WithMessage("Slug can only contain lowercase letters, numbers, and hyphens.");
    }
}

public class CreateEventCommandHandler(IApplicationDbContext dbContext, ITenantContext tenantContext) : IRequestHandler<CreateEventCommand, EventDto>
{
    public async Task<EventDto> Handle(CreateEventCommand request, CancellationToken cancellationToken)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug) 
            ? SlugHelper.Generate(request.Title) 
            : request.Slug;

        // Ensure uniqueness per tenant
        if (await dbContext.Events.AnyAsync(x => x.Slug == slug, cancellationToken))
        {
            // If generated, append a short random suffix
            if (string.IsNullOrWhiteSpace(request.Slug))
            {
                slug = $"{slug}-{Guid.NewGuid().ToString("N")[..4]}";
            }
            else
            {
                throw new InvalidOperationException($"Slug '{slug}' is already in use for this tenant.");
            }
        }

        var @event = new Event
        {
            Title = request.Title,
            Slug = slug,
            Description = request.Description,
            EventDate = request.EventDate,
            TenantId = tenantContext.TenantId ?? Guid.Empty
        };

        dbContext.Events.Add(@event);
        await dbContext.SaveChangesAsync(cancellationToken);

        return @event.Adapt<EventDto>();
    }
}
