using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Features.Tenants.Commands.CreateTenant;

public sealed record CreateTenantCommand(
    string Name, 
    string Identifier, 
    string AdminEmail,
    string AdminName,
    string? SupportEmail = null,
    string? MissionStatement = null,
    string? DefaultCurrency = "GHS",
    string? DefaultLocale = "en-GH") : IRequest<Guid>;

public class CreateTenantCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<CreateTenantCommand, Guid>
{
    public async Task<Guid> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Identifier = request.Identifier,
            SupportEmail = request.SupportEmail,
            MissionStatement = request.MissionStatement,
            DefaultCurrency = request.DefaultCurrency ?? "GHS",
            DefaultLocale = request.DefaultLocale ?? "en-GH",
            IsActive = true
        };

        dbContext.Tenants.Add(tenant);

        // Administration Onboarding
        var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            dbContext.Users, u => u.Email.ToLower() == request.AdminEmail.ToLower(), cancellationToken);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.AdminEmail,
                Name = request.AdminName,
                InviteStatus = UserInviteStatus.Pending,
                IsActive = false
            };
            dbContext.Users.Add(user);
        }

        var assignment = new UserScopeAssignment
        {
            Id = Guid.NewGuid(),
            User = user,
            Role = "TenantAdmin",
            ScopeType = ScopeType.Organisation,
            TargetId = tenant.Id
        };
        dbContext.UserScopeAssignments.Add(assignment);

        await dbContext.SaveChangesAsync(cancellationToken);

        return tenant.Id;
    }
}
