using Microsoft.AspNetCore.Identity;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Application.Common.Security;

public static class PinHasher
{
    private static readonly PasswordHasher<CollectorPin> _hasher = new();

    public static string HashPin(CollectorPin collectorPin, string pin)
    {
        return _hasher.HashPassword(collectorPin, pin);
    }

    public static (bool Verified, bool RehashNeeded) VerifyPin(CollectorPin collectorPin, string pin)
    {
        var result = _hasher.VerifyHashedPassword(collectorPin, collectorPin.PinHash, pin);
        
        return result switch
        {
            PasswordVerificationResult.Success => (true, false),
            PasswordVerificationResult.SuccessRehashNeeded => (true, true),
            _ => (false, false)
        };
    }
}
