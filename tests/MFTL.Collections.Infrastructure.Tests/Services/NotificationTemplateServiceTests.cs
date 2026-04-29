using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using MFTL.Collections.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace MFTL.Collections.Infrastructure.Tests.Services;

public class NotificationTemplateServiceTests
{
    private readonly IApplicationDbContext _db;
    private readonly ISmsTemplateService _renderer;
    private readonly NotificationTemplateService _sut;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();

    public NotificationTemplateServiceTests()
    {
        _db = Substitute.For<IApplicationDbContext>();
        _renderer = Substitute.For<ISmsTemplateService>();
        _renderer.Render(Arg.Any<string>(), Arg.Any<object>())
                 .Returns(ci => $"RENDERED:{ci.ArgAt<string>(0)}");
        _sut = new NotificationTemplateService(_db, _renderer, NullLogger<NotificationTemplateService>.Instance);
    }

    private IQueryable<NotificationTemplate> MockTemplates(params NotificationTemplate[] templates)
    {
        var data = templates.AsQueryable();
        var mockSet = Substitute.For<DbSet<NotificationTemplate>, IQueryable<NotificationTemplate>>();
        ((IQueryable<NotificationTemplate>)mockSet).Provider.Returns(data.Provider);
        ((IQueryable<NotificationTemplate>)mockSet).Expression.Returns(data.Expression);
        ((IQueryable<NotificationTemplate>)mockSet).ElementType.Returns(data.ElementType);
        ((IQueryable<NotificationTemplate>)mockSet).GetEnumerator().Returns(data.GetEnumerator());
        _db.NotificationTemplates.Returns(mockSet);
        return data;
    }

    private NotificationTemplate MakeTemplate(
        string key, NotificationChannel channel, Guid tenantId,
        Guid? branchId = null, bool isSystemDefault = false, bool isActive = true,
        string body = "Hello {{name}}", string? subject = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId = branchId,
            TemplateKey = key,
            Channel = channel,
            Name = "Test",
            Body = body,
            Subject = subject,
            IsActive = isActive,
            IsSystemDefault = isSystemDefault
        };

    [Fact]
    public async Task GetTemplate_ReturnsBranchTemplate_WhenBranchSpecificExists()
    {
        var system = MakeTemplate("k", NotificationChannel.Sms, Guid.Empty, isSystemDefault: true);
        var tenant = MakeTemplate("k", NotificationChannel.Sms, _tenantId);
        var branch = MakeTemplate("k", NotificationChannel.Sms, _tenantId, _branchId);
        MockTemplates(system, tenant, branch);

        var result = await _sut.GetTemplateAsync("k", NotificationChannel.Sms, _tenantId, _branchId);

        result.Should().Be(branch);
    }

    [Fact]
    public async Task GetTemplate_ReturnsTenantTemplate_WhenNoBranchSpecificExists()
    {
        var system = MakeTemplate("k", NotificationChannel.Sms, Guid.Empty, isSystemDefault: true);
        var tenant = MakeTemplate("k", NotificationChannel.Sms, _tenantId);
        MockTemplates(system, tenant);

        var result = await _sut.GetTemplateAsync("k", NotificationChannel.Sms, _tenantId, _branchId);

        result.Should().Be(tenant);
    }

    [Fact]
    public async Task GetTemplate_ReturnsSystemDefault_WhenNoTenantTemplateExists()
    {
        var system = MakeTemplate("k", NotificationChannel.Sms, Guid.Empty, isSystemDefault: true);
        MockTemplates(system);

        var result = await _sut.GetTemplateAsync("k", NotificationChannel.Sms, _tenantId, _branchId);

        result.Should().Be(system);
    }

    [Fact]
    public async Task GetTemplate_ReturnsNull_WhenNoActiveTemplateFound()
    {
        MockTemplates(); // empty

        var result = await _sut.GetTemplateAsync("missing.key", NotificationChannel.Email, _tenantId, null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RenderAsync_ReturnsNull_WhenNoTemplateFound()
    {
        MockTemplates();

        var result = await _sut.RenderAsync("missing", NotificationChannel.Sms, _tenantId, null, new { });

        result.Should().BeNull();
    }

    [Fact]
    public async Task RenderAsync_ReturnsRenderedBodyAndSubject_WhenTemplateFound()
    {
        var template = MakeTemplate("k", NotificationChannel.Email, _tenantId, subject: "Hi {{name}}");
        MockTemplates(template);

        var result = await _sut.RenderAsync("k", NotificationChannel.Email, _tenantId, null, new { name = "Alice" });

        result.Should().NotBeNull();
        result!.Value.Body.Should().StartWith("RENDERED:");
        result.Value.Subject.Should().StartWith("RENDERED:");
    }
}

// ─── SmsTemplateService Renderer Unit Tests ───────────────────────────────────

public class SmsTemplateServiceRendererTests
{
    private readonly SmsTemplateService _sut = new(NullLogger<SmsTemplateService>.Instance);

    [Fact]
    public void Render_ReplacesDoubleBraceVariables()
    {
        var result = _sut.Render("Hello {{name}}!", new { name = "Alice" });
        result.Should().Be("Hello Alice!");
    }

    [Fact]
    public void Render_LeavesUnknownVariablesUnchanged()
    {
        var result = _sut.Render("Hello {{name}} from {{place}}!", new { name = "Bob" });
        result.Should().Be("Hello Bob from {{place}}!");
    }

    [Fact]
    public void Render_HandlesNullValues_AsEmptyString()
    {
        var result = _sut.Render("Amount: {{amount}}", new Dictionary<string, object?> { ["amount"] = null });
        result.Should().Be("Amount: ");
    }

    [Fact]
    public void Render_HandlesDictionaryInput()
    {
        var vars = new Dictionary<string, string> { ["key"] = "value" };
        var result = _sut.Render("{{key}}", vars);
        result.Should().Be("value");
    }

    [Fact]
    public void Render_SkipsIndexerProperties()
    {
        // Dictionary<string,string> has an indexer — should not throw
        var vars = new Dictionary<string, string> { ["x"] = "1" };
        var act = () => _sut.Render("{{x}}", (object)vars);
        act.Should().NotThrow();
    }

    [Fact]
    public void Render_HandlesSingleBraceVariables()
    {
        var result = _sut.Render("Hello {name}!", new { name = "Carol" });
        result.Should().Be("Hello Carol!");
    }

    [Fact]
    public void Render_ReturnsEmptyString_WhenTemplateIsEmpty()
    {
        var result = _sut.Render("", new { name = "x" });
        result.Should().BeEmpty();
    }
}
