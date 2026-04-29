using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MFTL.Collections.Infrastructure.Services;

namespace MFTL.Collections.Infrastructure.Tests.Services;

public class SmsTemplateServiceTests
{
    private readonly SmsTemplateService _service = new(NullLogger<SmsTemplateService>.Instance);

    [Fact]
    public void Render_ReplacesSimpleVariables()
    {
        var result = _service.Render("Hi {{donorName}}, receipt {{receiptNumber}} for {{currency}} {{amount}}.", new
        {
            donorName = "Ama",
            receiptNumber = "REC-100",
            currency = "GHS",
            amount = 150.5m
        });

        result.Should().Be("Hi Ama, receipt REC-100 for GHS 150.5.");
    }

    [Fact]
    public void Render_HandlesMissingVariablesSafely()
    {
        var result = _service.Render("Hi {{donorName}}, receipt {{receiptNumber}}.", new { donorName = "Ama" });

        result.Should().Be("Hi Ama, receipt {{receiptNumber}}.");
    }

    [Fact]
    public void Render_HandlesNullValues()
    {
        var result = _service.Render("Phone: {{phone}}", new { phone = (string?)null });

        result.Should().Be("Phone: ");
    }

    [Fact]
    public void Render_SkipsIndexerProperties()
    {
        var model = new ModelWithIndexer();

        var result = _service.Render("Hello {{Name}}", model);

        result.Should().Be("Hello Indexer Safe");
    }

    [Fact]
    public void Render_HandlesDictionaryInput()
    {
        var result = _service.Render("Hi {{donorName}}", new Dictionary<string, object?>
        {
            ["donorName"] = "Kojo"
        });

        result.Should().Be("Hi Kojo");
    }

    [Fact]
    public void Render_HandlesJsonElementPayload()
    {
        using var json = JsonDocument.Parse("""{"donorName":"Efua","amount":75,"currency":"GHS"}""");

        var result = _service.Render("Hi {{donorName}}, amount {{currency}} {{amount}}", json.RootElement);

        result.Should().Be("Hi Efua, amount GHS 75");
    }

    [Fact]
    public void Render_SupportsLegacySingleBraceTemplates()
    {
        var result = _service.Render("Receipt {receiptNumber} for {donorName}", new
        {
            receiptNumber = "REC-200",
            donorName = "Yaw"
        });

        result.Should().Be("Receipt REC-200 for Yaw");
    }

    private sealed class ModelWithIndexer
    {
        public string Name => "Indexer Safe";

        public string this[int index] => $"Value {index}";
    }
}
