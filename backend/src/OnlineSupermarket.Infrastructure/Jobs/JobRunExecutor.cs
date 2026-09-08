using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OnlineSupermarket.Infrastructure.Jobs;

public sealed class JobRunExecutor(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<IntelligenceJobsOptions> options,
    ILogger<JobRunExecutor> logger)
{
    public async Task ExecuteAsync(JobRequest request, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");
        var leaseDuration = TimeSpan.FromMinutes(Math.Max(1, options.Value.LeaseMinutes));
        var now = DateTime.UtcNow;

        using var leaseScope = serviceScopeFactory.CreateScope();
        var store = leaseScope.ServiceProvider.GetRequiredService<IJobRunStore>();

        if (!await store.TryStartAsync(request.RunId, token, now.Add(leaseDuration), cancellationToken))
        {
            logger.LogDebug("Run {RunId} already claimed elsewhere; skipping", request.RunId);
            return;
        }

        using var handlerScope = serviceScopeFactory.CreateScope();
        var handler = handlerScope.ServiceProvider.GetServices<IBackgroundJobHandler>()
            .FirstOrDefault(h => h.JobName == request.JobName);

        if (handler is null)
        {
            await store.TryFailAsync(
                request.RunId, token,
                $"No handler registered for job '{request.JobName}'.",
                now, cancellationToken);
            logger.LogWarning("Run {RunId} failed: no handler registered for {JobName}", request.RunId, request.JobName);
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeatTask = RunHeartbeatAsync(store, request.RunId, token, leaseDuration, linkedCts);

        try
        {
            await handler.HandleAsync(request.RunId, linkedCts.Token);

            var succeeded = await store.TrySucceedAsync(request.RunId, token, DateTime.UtcNow, cancellationToken);
            if (!succeeded)
                logger.LogWarning("Run {RunId} lost ownership before completion; terminal not overwritten", request.RunId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host shutdown: leave Running so startup recovery owns the later decision.
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            // Lease was lost; recovery decides the terminal state. Do not overwrite.
            logger.LogWarning("Run {RunId} lease lost; handler cancelled", request.RunId);
        }
        catch (Exception error)
        {
            var sanitized = JobErrorSanitizer.Sanitize(error);
            logger.LogWarning("Run {RunId} failed: {Reason}", request.RunId, sanitized);
            var failed = await store.TryFailAsync(request.RunId, token, sanitized, DateTime.UtcNow, cancellationToken);
            if (!failed)
                logger.LogWarning("Run {RunId} already terminal; failure not overwritten", request.RunId);
        }
        finally
        {
            await linkedCts.CancelAsync();
            try
            {
                await heartbeatTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    internal TimeSpan HeartbeatIntervalOverride { get; set; } = TimeSpan.Zero;

    private async Task RunHeartbeatAsync(
        IJobRunStore store,
        Guid runId,
        string token,
        TimeSpan leaseDuration,
        CancellationTokenSource linkedCts)
    {
        var interval = HeartbeatIntervalOverride > TimeSpan.Zero
            ? HeartbeatIntervalOverride
            : TimeSpan.FromTicks(Math.Max(TimeSpan.FromSeconds(1).Ticks, leaseDuration.Ticks / 3));
        while (!linkedCts.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                var renewed = await store.TryRenewAsync(
                    runId, token, DateTime.UtcNow.Add(leaseDuration), linkedCts.Token);
                if (!renewed)
                {
                    logger.LogWarning("Run {RunId} lease lost; cancelling handler", runId);
                    await linkedCts.CancelAsync();
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception error)
            {
                logger.LogWarning("Run {RunId} heartbeat error: {Reason}", runId, JobErrorSanitizer.Sanitize(error));
            }
        }
    }
}