using MFTL.Collections.Domain.Common;
using Xunit;

namespace MFTL.Collections.Tests.Payments;

public class PaymentMethodMappingTests
{
    [Theory]
    [InlineData("cash", null, "Cash")]
    [InlineData("momo", null, "Mobile Money")]
    [InlineData("mobilemoney", null, "Mobile Money")]
    [InlineData("mobile_money", null, "Mobile Money")]
    [InlineData("moolre", null, "Mobile Money")]
    [InlineData("bank", null, "Bank Payment")]
    [InlineData("bank_debit", null, "Bank Payment")]
    [InlineData("directdebit", null, "Bank Payment")]
    [InlineData("direct_debit", null, "Bank Payment")]
    [InlineData("gocardless", null, "Bank Payment")]
    [InlineData("card", null, "Card")]
    [InlineData("mollie", null, "Card")]
    [InlineData("stripe", null, "Card")]
    [InlineData("paystack", null, "Card")]
    [InlineData(null, "gocardless", "Bank Payment")]
    [InlineData(null, "moolre", "Mobile Money")]
    [InlineData("unknown", null, "Unknown")]
    [InlineData(null, null, "Not specified")]
    public void Should_Map_To_Correct_Display_Label(string? method, string? provider, string expected)
    {
        // Act
        var result = PaymentMethodDisplayMapper.ToDisplayLabel(method, provider);

        // Assert
        Assert.Equal(expected, result);
    }
}
