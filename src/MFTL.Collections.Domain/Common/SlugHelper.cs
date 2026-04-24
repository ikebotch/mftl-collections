using System.Text.RegularExpressions;

namespace MFTL.Collections.Domain.Common;

public static class SlugHelper
{
    public static string Generate(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Guid.NewGuid().ToString("N")[..8];
        }

        var normalized = title.ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9\s-]", string.Empty);
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        normalized = normalized[..Math.Min(normalized.Length, 100)].Trim();
        normalized = Regex.Replace(normalized, @"\s", "-");

        return normalized;
    }
}
