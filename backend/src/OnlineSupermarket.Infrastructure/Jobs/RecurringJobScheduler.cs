using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OnlineSupermarket.Infrastructure.Jobs;

public class RecurringJobScheduler(
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    ILogger<RecurringJobScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                // Startup recovery: requeue queued runs lost with the in-process queue on
                // restart and fail stale Running leases. Keeps the DB the source of truth.
                await scope.ServiceProvider.GetRequiredService<JobLeaseService>()
                    .RecoverStaleJobsAsync(stoppingToken);

                var schedules = scope.ServiceProvider.GetServices<IRecurringJobSchedule>();
                var coordinator = scope.ServiceProvider.GetRequiredService<JobRunCoordinator>();

                foreach (var schedule in schedules)
                {
                    var dueJobs = await schedule.GetDueJobsAsync(
                        timeProvider.GetUtcNow().UtcDateTime,
                        stoppingToken);

                    foreach (var request in dueJobs)
                    {
                        await coordinator.TryQueueAsync(
                            request.JobName, request.LockKey, stoppingToken, request.BranchId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Recurring job scheduler iteration failed: {Reason}", JobErrorSanitizer.Sanitize(ex));
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}