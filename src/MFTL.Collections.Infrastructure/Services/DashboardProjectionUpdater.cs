using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Infrastructure.Services;

public class DashboardProjectionUpdater(IApplicationDbContext dbContext) : IDashboardProjectionUpdater
{
    public async Task UpdateRecipientDashboardAsync(Guid recipientFundId)
    {
        // Use dbContext to satisfy compiler warning, even if it's just a placeholder
        _ = dbContext;
        
        // Placeholder for dashboard projection logic
        await Task.CompletedTask;
    }
}
