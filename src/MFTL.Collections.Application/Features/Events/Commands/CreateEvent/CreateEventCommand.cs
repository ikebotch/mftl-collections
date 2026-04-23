using MFTL.Collections.Application.Common.Interfaces;
using MediatR;
using FluentValidation;

using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Contracts.Requests;
using Mapster;

namespace MFTL.Collections.Application.Features.Events.Commands.CreateEvent;

public record CreateEventCommand(string Title, string Description, DateTimeOffset? EventDate) : IRequest<EventDto>;

public class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public class CreateEventCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<CreateEventCommand, EventDto>
{
    public async Task<EventDto> Handle(CreateEventCommand request, CancellationToken cancellationToken)
    {
        var @event = new Event
        {
            Title = request.Title,
            Description = request.Description,
            EventDate = request.EventDate
        };

        dbContext.Events.Add(@event);
        await dbContext.SaveChangesAsync(cancellationToken);

        return @event.Adapt<EventDto>();
    }
}
