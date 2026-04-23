namespace MFTL.Collections.Application.Common.Interfaces;

public interface IDashboardProjectionUpdater
{
    Task UpdateRecipientDashboardAsync(Guid recipientFundId);
}
