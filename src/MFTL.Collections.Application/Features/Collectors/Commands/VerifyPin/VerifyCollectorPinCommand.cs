using MediatR;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Contracts.Responses;

namespace MFTL.Collections.Application.Features.Collectors.Commands.VerifyPin;

public record VerifyCollectorPinCommand(string Pin) : IRequest<CollectorPinStatusResponse>;

public class VerifyCollectorPinCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext) : IRequestHandler<VerifyCollectorPinCommand, CollectorPinStatusResponse>
{
    public async Task<CollectorPinStatusResponse> Handle(VerifyCollectorPinCommand request, CancellationToken cancellationToken)
    {
        var auth0Id = currentUserService.UserId;
        if (string.IsNullOrEmpty(auth0Id)) throw new UnauthorizedAccessException();

        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");

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

        var pin = await dbContext.CollectorPins
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.TenantId == tenantId, cancellationToken);

        if (pin == null)
        {
            return new CollectorPinStatusResponse(false, false);
        }

        var (isValid, rehashNeeded) = PinHasher.VerifyPin(pin, request.Pin);

        if (isValid)
        {
            pin.FailedAttempts = 0;
            pin.LastVerifiedAt = DateTimeOffset.UtcNow;
            
            if (rehashNeeded)
            {
                pin.PinHash = PinHasher.HashPin(pin, request.Pin);
            }
        }
        else
        {
            pin.FailedAttempts++;
            // Optional: Handle lockout here if failedAttempts > threshold
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CollectorPinStatusResponse(true, isValid);
    }
}
