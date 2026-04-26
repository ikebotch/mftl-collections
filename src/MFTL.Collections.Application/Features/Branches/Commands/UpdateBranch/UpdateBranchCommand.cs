using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Application.Features.Branches.Commands.UpdateBranch;

public record UpdateBranchCommand(
    Guid Id,
    string Name,
    string Identifier,
    string? Location,
    bool IsActive) : IRequest<bool>;

public class UpdateBranchCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<UpdateBranchCommand, bool>
{
    public async Task<bool> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await dbContext.Branches
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (branch == null) return false;

        branch.Name = request.Name;
        branch.Identifier = request.Identifier;
        branch.Location = request.Location;
        branch.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
