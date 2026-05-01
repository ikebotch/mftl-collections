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

    public Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody, string? plainTextBody = null, bool useDefaultWrapper = true)
    {
        logger.LogInformation(
            "[MOCK EMAIL] {Subject} → {ToEmail} ({ToName}). Wrapper: {UseWrapper}",
            subject, toEmail, toName, useDefaultWrapper);
        return Task.FromResult(true);
    }
}
