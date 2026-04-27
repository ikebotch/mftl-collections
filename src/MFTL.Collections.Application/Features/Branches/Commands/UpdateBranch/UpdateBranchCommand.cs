using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Application.Features.Branches.Commands.UpdateBranch;

public record UpdateBranchCommand(
    Guid Id,
    string? Name = null,
    string? Identifier = null,
    string? Location = null,
    bool? IsActive = null) : IRequest<bool>;

public class UpdateBranchCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<UpdateBranchCommand, bool>
{
    public async Task<bool> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await dbContext.Branches
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (branch == null) return false;

        if (request.Name != null) branch.Name = request.Name;
        if (request.Identifier != null) branch.Identifier = request.Identifier;
        if (request.Location != null) branch.Location = request.Location;
        if (request.IsActive.HasValue) branch.IsActive = request.IsActive.Value;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
