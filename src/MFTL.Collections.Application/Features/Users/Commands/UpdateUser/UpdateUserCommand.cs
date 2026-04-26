using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Users.Commands.UpdateUser;

public record UpdateUserCommand : IRequest<bool>
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? PhoneNumber { get; init; }
    public bool IsActive { get; init; }
}

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MinimumLength(2);
    }
}

public class UpdateUserCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<UpdateUserCommand, bool>
{
    public async Task<bool> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user == null)
        {
            return false;
        }

        user.Name = request.Name ?? user.Name;
        user.PhoneNumber = request.PhoneNumber ?? user.PhoneNumber;
        user.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
