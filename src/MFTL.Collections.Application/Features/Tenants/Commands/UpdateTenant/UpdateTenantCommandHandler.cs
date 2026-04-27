using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Application.Features.Tenants.Commands.UpdateTenant;

public class UpdateTenantCommandHandler(IApplicationDbContext context) : IRequestHandler<UpdateTenantCommand, bool>
{
    public async Task<bool> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (tenant == null) return false;

        tenant.Name = request.Name;
        tenant.SupportEmail = request.SupportEmail;

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
