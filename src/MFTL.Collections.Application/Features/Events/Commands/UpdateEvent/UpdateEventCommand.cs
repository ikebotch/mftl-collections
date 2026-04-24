using MFTL.Collections.Application.Common.Interfaces;
using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Common;
using MFTL.Collections.Contracts.Requests;
using Mapster;

namespace MFTL.Collections.Application.Features.Events.Commands.UpdateEvent;

public record UpdateEventCommand(Guid Id, string Title, string Description, DateTimeOffset? EventDate, bool IsActive, string? Slug = null) : IRequest<EventDto>;

public class UpdateEventCommandValidator : AbstractValidator<UpdateEventCommand>
{
    public UpdateEventCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Slug)
            .MaximumLength(100)
            .Matches(@"^[a-z0-9-]*$")
            .When(x => !string.IsNullOrWhiteSpace(x.Slug))
            .WithMessage("Slug can only contain lowercase letters, numbers, and hyphens.");
    }
}

public class UpdateEventCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<UpdateEventCommand, EventDto>
{
    public async Task<EventDto> Handle(UpdateEventCommand request, CancellationToken cancellationToken)
    {
        var @event = await dbContext.Events
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (@event == null)
        {
            throw new InvalidOperationException($"Event with ID '{request.Id}' not found.");
        }

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? SlugHelper.Generate(request.Title)
            : request.Slug!;

        if (await dbContext.Events.AnyAsync(x => x.Slug == slug && x.Id != request.Id, cancellationToken))
        {
            slug = $"{slug}-{Guid.NewGuid():N}"[..Math.Min(slug.Length + 9, 100)];
        }

        @event.Title = request.Title;
        @event.Description = request.Description;
        @event.EventDate = request.EventDate;
        @event.IsActive = request.IsActive;
        @event.Slug = slug;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new EventDto(
            @event.Id,
            @event.Title,
            @event.Description,
            @event.EventDate,
            @event.IsActive,
            0, // These will be filled by a separate query or computed in a real scenario
            0,
            0,
            0,
            @event.Slug);
    }
}
