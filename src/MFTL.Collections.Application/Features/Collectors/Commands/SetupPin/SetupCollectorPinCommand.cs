using System.Linq;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Collectors.Commands.SetupPin;

public record SetupCollectorPinCommand(string Pin) : IRequest<CollectorPinStatusResponse>;

public class SetupCollectorPinCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext) : IRequestHandler<SetupCollectorPinCommand, CollectorPinStatusResponse>
{
    public async Task<CollectorPinStatusResponse> Handle(SetupCollectorPinCommand request, CancellationToken cancellationToken)
    {
        var auth0Id = currentUserService.UserId;
        if (string.IsNullOrEmpty(auth0Id)) throw new UnauthorizedAccessException();

        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");

        // 1. Validate PIN format
        if (string.IsNullOrEmpty(request.Pin) || request.Pin.Length < 4 || request.Pin.Length > 6 || !request.Pin.All(char.IsDigit))
        {
            throw new ArgumentException("PIN must be 4-6 digits.");
        }

        // 2. Validate User & Collector Role in Tenant
        var user = await dbContext.Users
            .Include(u => u.ScopeAssignments)
            .FirstOrDefaultAsync(u => u.Auth0Id == auth0Id, cancellationToken);

        if (user == null) throw new KeyNotFoundException("User not found.");

        var hasCollectorRole = user.ScopeAssignments.Any(a => 
            a.Role == "Collector" && 
            (a.ScopeType == ScopeType.Tenant && a.TargetId == tenantId ||
             a.ScopeType == ScopeType.Branch && dbContext.Branches.Any(b => b.Id == a.TargetId && b.TenantId == tenantId) ||
             a.ScopeType == ScopeType.Event && dbContext.Events.Any(e => e.Id == a.TargetId && e.TenantId == tenantId) ||
             a.ScopeType == ScopeType.RecipientFund && dbContext.RecipientFunds.Any(f => f.Id == a.TargetId && f.TenantId == tenantId) ||
             a.ScopeType == ScopeType.Platform));

        if (!hasCollectorRole)
        {
            throw new UnauthorizedAccessException("User does not have collector access in this tenant.");
        }

        // 3. Setup/Update PIN
        var existingPin = await dbContext.CollectorPins
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.TenantId == tenantId, cancellationToken);

        if (existingPin == null)
        {
            var newPin = new CollectorPin
            {
                UserId = user.Id,
                TenantId = tenantId
            };
            newPin.PinHash = PinHasher.HashPin(newPin, request.Pin);
            dbContext.CollectorPins.Add(newPin);
        }
        else
        {
            existingPin.PinHash = PinHasher.HashPin(existingPin, request.Pin);
            existingPin.FailedAttempts = 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CollectorPinStatusResponse(true);
    }
}
