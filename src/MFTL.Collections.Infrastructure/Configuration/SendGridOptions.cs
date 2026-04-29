namespace MFTL.Collections.Infrastructure.Configuration;

public class SendGridOptions
{
    public const string SectionName = "SendGrid";

    public string? ApiKey { get; set; }
    public string FromEmail { get; set; } = "noreply@mftlcollections.com";
    public string FromName { get; set; } = "MFTL Collections";

    /// <summary>
    /// Optional SendGrid Dynamic Template ID.
    /// When set, emails are dispatched using the dynamic template with rendered
    /// subject/body passed as template_data variables.
    /// When absent, a plain HTML email is sent directly.
    /// </summary>
    public string? DefaultTemplateId { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
