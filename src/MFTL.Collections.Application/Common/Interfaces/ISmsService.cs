namespace MFTL.Collections.Application.Common.Interfaces;

public interface ISmsService
{
    Task<string> SendSmsAsync(string phoneNumber, string message);
    Task<decimal> GetBalanceAsync();
    Task<string> GetStatusAsync(string messageId);
}
