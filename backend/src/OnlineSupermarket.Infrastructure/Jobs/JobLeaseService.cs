namespace OnlineSupermarket.Infrastructure.Jobs;

public class JobLeaseService(IJobRunStore jobRunStore, IJobQueue jobQueue, TimeProvider timeProvider)
{
    public async Task RecoverStaleJobsAsync(CancellationToken cancellationToken)
    {
        // 1. Requeue queued runs that were never picked up (e.g. in-process queue was lost during restart).
        // A later claim is always atomic, so a duplicate requeue is harmless: only one worker wins the run.
        var queuedRequests = await jobRunStore.GetQueuedRequestsAsync(cancellationToken);
        foreach (var request in queuedRequests)
        {
            try
            {
                await jobQueue.EnqueueAsync(request, cancellationToken);
            }
            catch
            {
                // ignore channel failures; the run stays Queued and will be requeued next recovery pass
            }
        }

        // 2. Fail stale Running runs atomically by predicate so only an expired, unowned run flips to Failed.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await jobRunStore.FailExpiredAsync(now, "Lease expired and job abandoned", cancellationToken);
    }
}