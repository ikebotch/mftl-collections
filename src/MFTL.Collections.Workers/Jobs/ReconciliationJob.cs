using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MFTL.Collections.Workers.Jobs;

public class ReconciliationJob(ILogger<ReconciliationJob> logger)
{
    [Function("ReconciliationJob")]
    public void Run([TimerTrigger("0 0 * * * *")] TimerInfo timer)
    {
        logger.LogInformation("Running scheduled reconciliation job at: {Now}", DateTime.Now);
        // Implement reconciliation logic
    }
}
