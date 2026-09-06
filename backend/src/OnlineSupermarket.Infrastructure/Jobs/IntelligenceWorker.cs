using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OnlineSupermarket.Infrastructure.Jobs;

public class IntelligenceWorker(
    IJobQueue jobQueue,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<IntelligenceJobsOptions> options,
    ILogger<IntelligenceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var semaphore = new SemaphoreSlim(options.Value.MaxConcurrentJobs);
        var tasks = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = await jobQueue.DequeueAsync(stoppingToken);

                await semaphore.WaitAsync(stoppingToken);

                var task = Task.Run(() => ProcessAsync(request, semaphore, stoppingToken), stoppingToken);
                tasks.Add(task);
                tasks.RemoveAll(t => t.IsCompleted);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error dequeuing job");
                await Task.Delay(1000, stoppingToken);
            }
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProcessAsync(JobRequest request, SemaphoreSlim semaphore, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IJobRunStore>();

            var token = Guid.NewGuid().ToString("N");
            var leaseDuration = TimeSpan.FromMinutes(Math.Max(1, options.Value.LeaseMinutes));
            var leaseExpiresAtUtc = DateTime.UtcNow.Add(leaseDuration);

            if (!await store.TryClaimAsync(request.RunId, token, leaseExpiresAtUtc, stoppingToken))
            {
                logger.LogDebug("Run {RunId} already claimed elsewhere; skipping", request.RunId);
                return;
            }

            var handler = scope.ServiceProvider.GetServices<IBackgroundJobHandler>()
                .FirstOrDefault(h => h.JobName == request.JobName);

            if (handler is null)
            {
                logger.LogWarning("No handler found for job {JobName} ({RunId})", request.JobName, request.RunId);
                await store.TryCompleteAsync(
                    request.RunId, token, false,
                    $"No handler registered for job '{request.JobName}'.",
                    DateTime.UtcNow, stoppingToken);
                return;
            }

            using var renewCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var renewTask = RenewLeaseLoopAsync(store, request.RunId, token, leaseDuration, renewCts.Token);

            try
            {
                await handler.HandleAsync(request.RunId, stoppingToken);
                await store.TryCompleteAsync(request.RunId, token, true, null, DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown: leave Running so startup recovery fails it once the lease expires.
            }
            catch (Exception ex)
            {
                var sanitized = JobErrorSanitizer.Sanitize(ex);
                logger.LogError(ex, "Job {JobName} ({RunId}) failed", request.JobName, request.RunId);
                await store.TryCompleteAsync(request.RunId, token, false, sanitized, DateTime.UtcNow, stoppingToken);
            }
            finally
            {
                await renewCts.CancelAsync();
                try
                {
                    await renewTask;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing job {JobName} ({RunId})", request.JobName, request.RunId);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task RenewLeaseLoopAsync(
        IJobRunStore store,
        Guid runId,
        string token,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var renewEvery = TimeSpan.FromTicks(leaseDuration.Ticks / 2);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(renewEvery, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var renewed = await store.TryRenewAsync(
                runId, token, DateTime.UtcNow.Add(leaseDuration), cancellationToken);
            if (!renewed)
            {
                return;
            }
        }
    }
}