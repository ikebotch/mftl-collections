using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Infrastructure.Services;

public sealed class ReceiptNumberGenerator : IReceiptNumberGenerator
{
    public string Generate()
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        var suffix = Convert.ToHexString(Guid.NewGuid().ToByteArray()[..3]);
        return $"RCT-{timestamp}-{suffix}";
    }
}
