using MFTL.Collections.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace MFTL.Collections.Infrastructure.Services;

public class MockEmailService(ILogger<MockEmailService> logger) : IEmailService
{
    public async Task SendInvitationAsync(string email, string name, string role)
    {
        await Task.Delay(100);
        logger.LogInformation(
            "[MOCK EMAIL] Invitation → {Email} ({Name}) | Role: {Role}",
            email, name, role);
    }

    public Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        logger.LogInformation(
            "[MOCK EMAIL] {Subject} → {ToEmail} ({ToName})",
            subject, toEmail, toName);
        return Task.FromResult(true);
    }
}
