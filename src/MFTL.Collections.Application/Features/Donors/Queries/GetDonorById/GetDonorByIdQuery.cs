using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Requests;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Donors.Queries.GetDonorById;

public record GetDonorByIdQuery(Guid Id) : IRequest<DonorDto?>;

public class GetDonorByIdQueryHandler(IApplicationDbContext dbContext) : IRequestHandler<GetDonorByIdQuery, DonorDto?>
{
    public async Task<DonorDto?> Handle(GetDonorByIdQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Contributors
            .Where(c => c.Id == request.Id)
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
            .FirstOrDefaultAsync(cancellationToken);
    }
}
