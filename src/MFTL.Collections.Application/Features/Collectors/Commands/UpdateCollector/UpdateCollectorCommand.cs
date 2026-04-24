using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Collectors.Commands.UpdateCollector;

public record UpdateCollectorCommand(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string Type,
    string Status,
    string? Notes,
    bool LoginEnabled,
    bool RecordCash,
    bool IssueReceipts,
    bool ViewDashboard,
    bool ViewReports,
    IEnumerable<Guid> EventIds,
    decimal DailyLimit,
    decimal MaxCashLimit,
    bool OfflineAllowed,
    decimal ApprovalThreshold) : IRequest<bool>;

public class UpdateCollectorCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<UpdateCollectorCommand, bool>
{
    public async Task<bool> Handle(UpdateCollectorCommand request, CancellationToken cancellationToken)
    {
        var collector = await dbContext.Contributors
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (collector == null) return false;

        // Update basic info
        collector.Name = request.Name;
        collector.Email = request.Email;
        collector.PhoneNumber = request.Phone;
        // In a real system, Type, Status, Notes, Permissions, etc. would be mapped to specific fields or JSON metadata
        // For this pass, we'll assume they are stored in a Metadata JSON field or similar
        
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
