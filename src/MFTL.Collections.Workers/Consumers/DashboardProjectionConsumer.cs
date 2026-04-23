using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Workers.Consumers;

public class DashboardProjectionConsumer(IDashboardProjectionUpdater updater, ILogger<DashboardProjectionConsumer> logger)
{
    [Function("UpdateDashboardProjection")]
    public async Task Run([QueueTrigger("dashboard-updates")] string message)
    {
        logger.LogInformation("Processing dashboard update: {Message}", message);
        // In a real app, parse message and call updater
        // await updater.UpdateRecipientDashboardAsync(Guid.Parse(message));
    }
}
