using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace MFTL.Collections.Infrastructure.Services;

/// <summary>
/// Delivers transactional emails via the SendGrid Web API.
/// When <see cref="SendGridOptions.DefaultTemplateId"/> is present, the rendered
/// subject and body are forwarded as dynamic template data variables so the
/// SendGrid template acts purely as a presentation wrapper — no content is
/// hardcoded here.
/// Falls back to a plain HTML single-send when no template ID is configured.
/// </summary>
public class SendGridEmailService(
    IOptions<SendGridOptions> options,
    ILogger<SendGridEmailService> logger) : IEmailService
{
    private readonly SendGridOptions _opts = options.Value;

    public async Task SendInvitationAsync(string email, string name, string role)
    {
        var subject = $"You've been invited to {_opts.FromName}";
        var htmlBody = $"""
            <p>Hello {name},</p>
            <p>You have been invited to <strong>{_opts.FromName}</strong> as a <strong>{role}</strong>.</p>
            <p>Please log in to accept your invitation and complete setup.</p>
            <br/>
            <p>The {_opts.FromName} Team</p>
            """;

        var sent = await SendAsync(email, name, subject, htmlBody);
        if (!sent)
        {
            logger.LogWarning(
                "Invitation email to {Email} failed. SendGrid rejected the request.", email);
        }
    }

    public async Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody, string? plainTextBody = null, bool useDefaultWrapper = true)
    {
        if (!_opts.IsConfigured)
        {
            logger.LogWarning(
                "SendGrid API key is not configured. Email to {ToEmail} was not sent.", toEmail);
            return false;
        }

        logger.LogInformation("Sending email from: {FromEmail}", _opts.FromEmail);

        var client = new SendGridClient(_opts.ApiKey);
        var from = new EmailAddress(_opts.FromEmail, _opts.FromName);
        var to = new EmailAddress(toEmail, toName);

        SendGridMessage message;
        var finalPlainText = plainTextBody ?? HtmlToPlainText(htmlBody);

        // More robust full HTML detection: trim and look for doctype or html tag
        var trimmedBody = htmlBody.Trim();
        bool isFullHtml = trimmedBody.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase) || 
                          trimmedBody.StartsWith("<html", StringComparison.OrdinalIgnoreCase);


        if (!string.IsNullOrWhiteSpace(_opts.DefaultTemplateId) && useDefaultWrapper && !isFullHtml)
        {
            // Use SendGrid Dynamic Template — pass rendered content as template data.
            message = new SendGridMessage
            {
                From = from,
                TemplateId = _opts.DefaultTemplateId,
                PlainTextContent = finalPlainText
            };
            message.AddTo(to);
            message.SetTemplateData(new
            {
                subject,
                name = toName,
                messageTitle = subject,
                messageBody = htmlBody,
                footerText = $"© {DateTime.UtcNow.Year} {_opts.FromName}. All rights reserved."
            });
        }
        else
        {
            // Single send with both text and html parts
            message = MailHelper.CreateSingleEmail(from, to, subject, finalPlainText, htmlBody);
        }

        try
        {
            var response = await client.SendEmailAsync(message);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Email '{Subject}' dispatched to {ToEmail} via SendGrid. Status: {StatusCode}",
                    subject, toEmail, (int)response.StatusCode);
                return true;
            }

            var body = await response.Body.ReadAsStringAsync();
            logger.LogError(
                "SendGrid rejected email to {ToEmail}. Status: {StatusCode}. Body: {Body}",
                toEmail, (int)response.StatusCode, body);

            // 4xx = bad request / auth — retrying won't help; surface as false
            // 5xx = transient SendGrid error — returning false allows OutboxProcessor to retry
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unexpected error sending email '{Subject}' to {ToEmail}.",
                subject, toEmail);
            return false;
        }
    }

    private static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        // Simple HTML to plain text conversion:
        // 1. Replace <br/>, <p>, <div> with newlines
        // 2. Strip all remaining tags
        // 3. Decode common entities

        var text = html;
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<(br|p|div)[^>]*>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]*>", string.Empty);
        text = System.Net.WebUtility.HtmlDecode(text);
        
        // Cleanup multiple newlines
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n\s*\n", "\n\n");
        
        return text.Trim();
    }
}
