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
        RuleFor(x => x.Slug)
            .MaximumLength(100)
            .Matches(@"^[a-z0-9-]*$")
            .When(x => !string.IsNullOrWhiteSpace(x.Slug))
            .WithMessage("Slug can only contain lowercase letters, numbers, and hyphens.");
    }
}

public class CreateEventCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<CreateEventCommand, EventDto>
{
    public async Task<EventDto> Handle(CreateEventCommand request, CancellationToken cancellationToken)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? SlugHelper.Generate(request.Title)
            : request.Slug!;

        if (await dbContext.Events.AnyAsync(x => x.Slug == slug, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(request.Slug))
            {
                slug = $"{slug}-{Guid.NewGuid():N}"[..Math.Min(slug.Length + 9, 100)];
            }
            else
            {
                throw new InvalidOperationException($"Slug '{slug}' is already in use for this tenant.");
            }
        }

        var @event = new Event
        {
            Title = request.Title,
            Description = request.Description,
            EventDate = request.EventDate,
            Slug = slug,
        };

        dbContext.Events.Add(@event);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new EventDto(
            @event.Id,
            @event.Title,
            @event.Description,
            @event.EventDate,
            @event.IsActive,
            0,
            0,
            0,
            0,
            @event.Slug);
    }
}
