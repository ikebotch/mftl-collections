using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using Mapster;

namespace MFTL.Collections.Application.Features.Public.Queries.ListPublicRecipientFunds;

public record ListPublicRecipientFundsQuery(string Slug) : IRequest<List<PublicRecipientFundDto>>;

public class ListPublicRecipientFundsQueryHandler(IApplicationDbContext dbContext, ITenantContext tenantContext) : IRequestHandler<ListPublicRecipientFundsQuery, List<PublicRecipientFundDto>>
{
    public async Task<List<PublicRecipientFundDto>> Handle(ListPublicRecipientFundsQuery request, CancellationToken cancellationToken)
    {
        var eventQuery = dbContext.Events
            .IgnoreQueryFilters()
            .Where(x => x.Slug == request.Slug && x.IsActive);

        if (tenantContext.TenantId.HasValue)
        {
            eventQuery = eventQuery.Where(x => x.TenantId == tenantContext.TenantId.Value);
        }

        var eventId = await eventQuery
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (eventId == null) return new List<PublicRecipientFundDto>();

        var funds = await dbContext.RecipientFunds
            .IgnoreQueryFilters()
            .Where(x => x.EventId == eventId)
            .ToListAsync(cancellationToken);

        return funds.Adapt<List<PublicRecipientFundDto>>();
    }
}
