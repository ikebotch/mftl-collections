namespace MFTL.Collections.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendInvitationAsync(string email, string name, string role);
    Task SendReceiptAsync(string email, string name, string amount, string currency, string receiptNumber, string eventTitle);
}
