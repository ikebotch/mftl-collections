using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Application.Features.Branches.Commands.DeleteBranch;

public record DeleteBranchCommand(Guid Id) : IRequest<bool>;

public class DeleteBranchCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<DeleteBranchCommand, bool>
{
    public async Task<bool> Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await dbContext.Branches
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (branch == null) return false;

        // Check for linked entities before deleting
        var hasLinkedEvents = await dbContext.Events.AnyAsync(e => e.BranchId == request.Id, cancellationToken);
        if (hasLinkedEvents)
        {
            // If it has events, we just deactivate it instead of deleting
            branch.IsActive = false;
        }
        else
        {
            dbContext.Branches.Remove(branch);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
