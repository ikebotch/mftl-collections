using MFTL.Collections.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace MFTL.Collections.Infrastructure.Services;

public class MockSmsService(ILogger<MockSmsService> logger) : ISmsService
{
    public async Task SendSmsAsync(string phoneNumber, string message)
    {
        // Simulate SMS sending delay
        await Task.Delay(200);
        
        logger.LogInformation("SIMULATED SMS: To {PhoneNumber}. Content: {Message}", 
            phoneNumber, message);
    }
}
