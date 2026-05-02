namespace MFTL.Collections.Domain.Common;

public static class PaymentMethodDisplayMapper
{
    public static string ToDisplayLabel(string? method, string? provider = null)
    {
        var effectiveMethod = (method ?? string.Empty).Trim().ToLowerInvariant();
        var effectiveProvider = (provider ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(effectiveMethod) && string.IsNullOrEmpty(effectiveProvider))
        {
            return "Not specified";
        }

        // 1. Try mapping the method first as it's the primary indicator
        var mappedMethod = MapMethod(effectiveMethod);
        if (mappedMethod != null) return mappedMethod;

        // 2. Try mapping the provider if method didn't match
        var mappedProvider = MapMethod(effectiveProvider);
        if (mappedProvider != null) return mappedProvider;

        // 3. Fallback to title-cased method or provider
        var fallback = !string.IsNullOrEmpty(effectiveMethod) ? effectiveMethod : effectiveProvider;
        return char.ToUpper(fallback[0]) + fallback[1..];
    }

    private static string? MapMethod(string input)
    {
        return input switch
        {
            "cash" => "Cash",
            "momo" or "mobilemoney" or "mobile_money" or "moolre" => "Mobile Money",
            "bank" or "bank_debit" or "directdebit" or "direct_debit" or "gocardless" => "Bank Payment",
            "card" or "mollie" or "stripe" or "paystack" => "Card",
            _ => null
        };
    }
}
