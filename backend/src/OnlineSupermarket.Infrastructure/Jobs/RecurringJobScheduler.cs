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
                var schedules = scope.ServiceProvider.GetServices<IRecurringJobSchedule>();
                var coordinator = scope.ServiceProvider.GetRequiredService<JobRunCoordinator>();

                foreach (var schedule in schedules)
                {
                    var dueJobs = await schedule.GetDueJobsAsync(
                        timeProvider.GetUtcNow().UtcDateTime,
                        stoppingToken);

                    foreach (var request in dueJobs)
                    {
                        await coordinator.TryQueueAsync(request.JobName, request.LockKey, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Recurring job scheduler iteration failed");
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