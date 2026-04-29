using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Features.Branches.Commands.CreateBranch;

[HasPermission("branches.create")]
public record CreateBranchCommand(
    Guid TenantId,
    string Name,
    string Identifier,
    string? Location,
    bool IsActive = true) : IRequest<Guid>, IHasScope
{
    public Guid? GetScopeId() => TenantId;
}

public class CreateBranchCommandHandler(
    IApplicationDbContext dbContext,
    IAccessPolicyResolver policyResolver) : IRequestHandler<CreateBranchCommand, Guid>
{
    public async Task<Guid> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var policy = await policyResolver.ResolvePolicyAsync();
        if (!policy.CanAccessTenant(request.TenantId))
        {
            throw new UnauthorizedAccessException("You do not have access to create branches in the specified organization.");
        }

        var branch = new Branch
        {
            TenantId = request.TenantId,
            Name = request.Name,
            Identifier = request.Identifier,
            Location = request.Location,
            IsActive = request.IsActive
        };

        dbContext.Branches.Add(branch);
        await dbContext.SaveChangesAsync(cancellationToken);

        return branch.Id;
    }
}
