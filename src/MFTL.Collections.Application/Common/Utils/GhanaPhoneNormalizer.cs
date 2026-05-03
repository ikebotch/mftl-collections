using System.Text.RegularExpressions;

namespace MFTL.Collections.Application.Common.Utils;

public static class GhanaPhoneNormalizer
{
    public static string Normalize(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        // Remove all non-digit characters
        var digits = Regex.Replace(phone, @"\D", "");

        // Handle +2330... or 2330... (country code + leading zero)
        if (digits.StartsWith("2330") && digits.Length == 13)
        {
            // Remove the redundant '0' after '233' -> '233244...'
            digits = "233" + digits[4..];
        }

        // Handle 0... (local 10-digit)
        if (digits.StartsWith("0") && digits.Length == 10)
        {
            // Convert 0244... to 233244... for consistency if preferred, 
            // but the user suggested 0244199324 for accountnumber.
            // I will return the 10-digit format starting with 0 as requested.
            return digits;
        }

        // Handle 233... (country code 12-digit)
        if (digits.StartsWith("233") && digits.Length == 12)
        {
            // Convert to 0... format: 233244... -> 0244...
            return "0" + digits[3..];
        }

        // Handle 244... (9-digit without leading 0)
        if (digits.Length == 9 && (digits.StartsWith("2") || digits.StartsWith("5")))
        {
            return "0" + digits;
        }

        // If it's already 10 digits starting with 0, return it
        if (digits.Length == 10 && digits.StartsWith("0"))
        {
            return digits;
        }

        return digits; // Return as-is if unrecognized but stripped of non-digits
    }

    public static bool IsValid(string phone)
    {
        var normalized = Normalize(phone);
        return normalized.Length == 10 && normalized.StartsWith("0");
    }
}
