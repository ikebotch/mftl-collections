using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Storefront.Queries.GetContributionStatus;

public record GetStorefrontContributionStatusQuery(Guid ContributionId) : IRequest<StorefrontContributionStatusDto>;

public class GetStorefrontContributionStatusQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetStorefrontContributionStatusQuery, StorefrontContributionStatusDto>
{
    public async Task<StorefrontContributionStatusDto> Handle(GetStorefrontContributionStatusQuery request, CancellationToken cancellationToken)
    {
        var contribution = await dbContext.Contributions
            .IgnoreQueryFilters()
            .Include(c => c.Payment)
            .FirstOrDefaultAsync(c => c.Id == request.ContributionId, cancellationToken);

        if (contribution == null)
        {
            throw new KeyNotFoundException($"Contribution with ID '{request.ContributionId}' not found.");
        }

        return new StorefrontContributionStatusDto(
            contribution.Id,
            contribution.Status.ToString(),
            contribution.Payment?.Status.ToString(),
            contribution.Payment?.ProviderReference,
            contribution.Payment?.FailureReason);
    }
}
