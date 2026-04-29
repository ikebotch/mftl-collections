using MFTL.Collections.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace MFTL.Collections.Infrastructure.Services;

public class MockEmailService(ILogger<MockEmailService> logger) : IEmailService
{
    public async Task SendInvitationAsync(string email, string name, string role)
    {
        // Simulate email sending delay
        await Task.Delay(500);
        
        logger.LogInformation("SIMULATED EMAIL: To {Email}, Name {Name}. Content: You have been invited to MFTL Collections as a {Role}.", 
            email, name, role);
            
        // In a real scenario, we'd use SendGrid, Mailtrap, etc.
    }

    public Task SendReceiptAsync(string email, string name, string amount, string currency, string receiptNumber, string eventTitle)
    {
        logger.LogInformation("Sending receipt to {Email}. Amount: {Currency} {Amount}. Receipt: {ReceiptNumber}", email, currency, amount, receiptNumber);
        return Task.CompletedTask;
    }

    public Task SendEmailAsync(string email, string subject, string body)
    {
        logger.LogInformation("Sending email to {Email}. Subject: {Subject}. Body: {Body}", email, subject, body);
        return Task.CompletedTask;
    }
}
