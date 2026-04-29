using MFTL.Collections.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace MFTL.Collections.Infrastructure.Services;

public class MockSmsService(ILogger<MockSmsService> logger) : ISmsService
{
    public async Task<string> SendSmsAsync(string phoneNumber, string message)
    {
        // Simulate SMS sending delay
        await Task.Delay(200);
        
        var messageId = Guid.NewGuid().ToString("N");
        logger.LogInformation("SIMULATED SMS: To {PhoneNumber}. Content: {Message}. MessageId: {MessageId}", 
            phoneNumber, message, messageId);

        return messageId;
    }

    public Task<decimal> GetBalanceAsync()
    {
        return Task.FromResult(100.00m);
    }

    public Task<string> GetStatusAsync(string messageId)
    {
        return Task.FromResult("Delivered");
    }
}
