using FluentAssertions;
using MFTL.Collections.Infrastructure.Services;

namespace MFTL.Collections.Infrastructure.Tests.Receipts;

public class ReceiptNumberGeneratorTests
{
    [Fact]
    public void Generate_ReturnsUniqueHumanReadableReceiptNumbers()
    {
        var generator = new ReceiptNumberGenerator();

        var receiptNumbers = Enumerable.Range(0, 100)
            .Select(_ => generator.Generate())
            .ToList();

        receiptNumbers.Should().OnlyHaveUniqueItems();
        receiptNumbers.Should().OnlyContain(number => number.StartsWith("RCT-"));
    }
}
