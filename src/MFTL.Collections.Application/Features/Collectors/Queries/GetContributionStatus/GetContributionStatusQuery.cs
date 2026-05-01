using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Exceptions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Collectors.Queries.GetContributionStatus;

public record GetContributionStatusQuery(Guid ContributionId) : IRequest<ContributionStatusDto>;

public class GetContributionStatusQueryHandler(
    IApplicationDbContext dbContext,
    IScopeAccessService scopeService,
    ITenantContext tenantContext) : IRequestHandler<GetContributionStatusQuery, ContributionStatusDto>
{
    public async Task<ContributionStatusDto> Handle(GetContributionStatusQuery request, CancellationToken cancellationToken)
    {
        // 1. Fetch contribution
        var contribution = await dbContext.Contributions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Receipt)
            .FirstOrDefaultAsync(x => x.Id == request.ContributionId, cancellationToken);

        if (contribution == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Contribution), request.ContributionId);
        }

        // 2. Validate tenant
        if (tenantContext.TenantId != contribution.TenantId)
        {
            throw new ForbiddenAccessException("You do not have access to this tenant.");
        }

        // 3. Validate collector access (assigned to event or fund)
        // We check for contributions.create or payments.initiate as surrogate permissions for "being a collector for this contribution"
        var hasAccess = await scopeService.CanAccessAsync(Permissions.Contributions.Create, contribution.TenantId, eventId: contribution.EventId, cancellationToken: cancellationToken) ||
                        await scopeService.CanAccessAsync(Permissions.Payments.Initiate, contribution.TenantId, eventId: contribution.EventId, cancellationToken: cancellationToken) ||
                        await scopeService.CanAccessAsync(Permissions.Contributions.Create, contribution.TenantId, fundId: contribution.RecipientFundId, cancellationToken: cancellationToken) ||
                        await scopeService.CanAccessAsync(Permissions.Payments.Initiate, contribution.TenantId, fundId: contribution.RecipientFundId, cancellationToken: cancellationToken);

        if (!hasAccess)
        {
            throw new ForbiddenAccessException("You do not have access to this contribution status.");
        }

        return new ContributionStatusDto(
            contribution.Id,
            contribution.Status.ToString(),
            contribution.Receipt?.Id,
            contribution.Method,
            contribution.Amount,
            contribution.Currency);
    }
}
