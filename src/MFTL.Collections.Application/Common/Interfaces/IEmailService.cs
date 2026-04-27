namespace MFTL.Collections.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendInvitationAsync(string email, string name, string role);
}
