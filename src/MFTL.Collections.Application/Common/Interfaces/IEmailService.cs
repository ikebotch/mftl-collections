namespace MFTL.Collections.Application.Common.Interfaces;

public interface IEmailService
{
    /// <summary>Sends an invitation email to a new user.</summary>
    Task SendInvitationAsync(string email, string name, string role);

    /// <summary>
    /// Sends a transactional email with pre-rendered subject and HTML body.
    /// Returns true if the email was accepted for delivery; false otherwise.
    /// Callers (e.g. OutboxProcessor) should treat false as retriable.
    /// </summary>
    Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody);
}
