using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Donors.Queries.ListDonors;

public record ListDonorsQuery() : IRequest<IEnumerable<DonorDto>>;

public class ListDonorsQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<ListDonorsQuery, IEnumerable<DonorDto>>
{
    public async Task<IEnumerable<DonorDto>> Handle(ListDonorsQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Contributors
            .Select(c => new DonorDto(
                c.Id,
                c.Name,
                c.Email,
                c.PhoneNumber,
                c.IsAnonymous,
                dbContext.Contributions.Where(con => con.ContributorId == c.Id).Sum(con => con.Amount),
                dbContext.Contributions.Count(con => con.ContributorId == c.Id),
                dbContext.Contributions.Where(con => con.ContributorId == c.Id).Max(con => con.CreatedAt)
            ))
            .ToListAsync(cancellationToken);
    }
}
