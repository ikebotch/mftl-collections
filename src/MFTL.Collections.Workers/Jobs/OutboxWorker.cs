using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Infrastructure.Services;

namespace MFTL.Collections.Workers.Jobs;

public sealed class OutboxWorker(IOutboxProcessor outboxProcessor, ILogger<OutboxWorker> logger)
{
    [Function("ProcessOutbox")]
    public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        logger.LogInformation("OutboxWorker starting at: {Time}", DateTimeOffset.UtcNow);
        
        try
        {
            await outboxProcessor.ProcessMessagesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred during outbox processing.");
        }
    }
}
