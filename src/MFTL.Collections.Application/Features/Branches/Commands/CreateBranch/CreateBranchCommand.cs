using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Features.Branches.Commands.CreateBranch;

public record CreateBranchCommand(
    Guid TenantId,
    string Name,
    string Identifier,
    string? Location,
    bool IsActive = true) : IRequest<Guid>;

public class CreateBranchCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<CreateBranchCommand, Guid>
{
    public async Task<Guid> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
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
