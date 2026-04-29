using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Users.Commands.UpdateUser;

[HasPermission("users.update")]
public record UpdateUserCommand : IRequest<bool>, IHasScope
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? PhoneNumber { get; init; }
    public bool IsActive { get; init; }
    public bool? IsPlatformAdmin { get; init; }

    public Guid? GetScopeId() => null; // User level updates are globally scoped but filtered by policy
}

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.Name).NotEmpty().MinimumLength(2);
    }
}

public class UpdateUserCommandHandler(
    IApplicationDbContext dbContext,
    IAccessPolicyResolver policyResolver,
    ICurrentUserService currentUserService) : IRequestHandler<UpdateUserCommand, bool>
{
    public async Task<bool> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var policy = await policyResolver.ResolvePolicyAsync();
        var user = await policy.FilterUsers(dbContext.Users.AsQueryable())
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found or you do not have permission to manage this user.");
        }

        // Security check: Only platform admins can change IsPlatformAdmin
        if (request.IsPlatformAdmin.HasValue && request.IsPlatformAdmin != user.IsPlatformAdmin)
        {
            if (!currentUserService.IsPlatformAdmin)
            {
                throw new UnauthorizedAccessException("Only Platform Administrators can modify the system-wide admin status.");
            }
            user.IsPlatformAdmin = request.IsPlatformAdmin.Value;
        }

        user.Name = request.Name ?? user.Name;
        user.PhoneNumber = request.PhoneNumber ?? user.PhoneNumber;
        user.IsActive = request.IsActive;
        
        user.ModifiedAt = DateTimeOffset.UtcNow;
        user.ModifiedBy = currentUserService.UserId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
