using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Collectors.Queries.GetCollectorMe;
using MFTL.Collections.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Collectors.Commands.CreateCollector;

public record CreateCollectorCommand(
    string Name,
    string Email,
    string PhoneNumber) : IRequest<CollectorMeDto>;

public class CreateCollectorCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<CreateCollectorCommand, CollectorMeDto>
{
    public async Task<CollectorMeDto> Handle(CreateCollectorCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (existingUser != null)
        {
            throw new InvalidOperationException($"A user with email {request.Email} already exists.");
        }

        var user = new User
        {
            Name = request.Name,
            Email = request.Email,
            Auth0Id = $"dev|{Guid.NewGuid():N}", // Mock Auth0Id for dev
            IsActive = true,
            IsPlatformAdmin = false
        };

        // Add a default collector role assignment if needed, or just let the admin do it later
        // For the sake of the demo/wizard, we can add a platform-level "Collector" assignment
        user.ScopeAssignments.Add(new UserScopeAssignment
        {
            ScopeType = ScopeType.Platform,
            Role = "Collector"
        });

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CollectorMeDto(
            user.Id,
            user.Name,
            user.Email,
            "Active",
            0,
            0,
            0,
            0,
            null,
            false,
            null);
    }
}
