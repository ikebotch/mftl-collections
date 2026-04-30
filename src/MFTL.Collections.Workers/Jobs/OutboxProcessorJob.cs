using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Workers.Jobs;

public sealed class OutboxProcessorJob(IOutboxProcessor outboxProcessor, ILogger<OutboxProcessorJob> logger)
{
    [Function("OutboxProcessorJob")]
    public async Task Run([TimerTrigger("*/30 * * * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        var processed = await outboxProcessor.ProcessMessagesAsync(cancellationToken: cancellationToken);
        logger.LogInformation("OutboxProcessorJob completed. Processed {Count} messages.", processed);
    }
}
