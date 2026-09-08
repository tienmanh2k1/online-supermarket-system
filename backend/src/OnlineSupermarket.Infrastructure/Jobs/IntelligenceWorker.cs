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
    private readonly SemaphoreSlim _semaphore = new(Math.Max(1, options.Value.MaxConcurrentJobs));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var active = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            JobRequest request;
            try
            {
                request = await jobQueue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogWarning("Unable to dequeue job: {Reason}", JobErrorSanitizer.Sanitize(error));
                await Task.Delay(1000, stoppingToken);
                continue;
            }

            await _semaphore.WaitAsync(stoppingToken);

            var task = Task.Run(
                () => RunJobAsync(request, stoppingToken),
                CancellationToken.None);
            active.Add(task);
            active.RemoveAll(t => t.IsCompleted);
        }

        try
        {
            await Task.WhenAll(active);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunJobAsync(JobRequest request, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<JobRunExecutor>();
            await executor.ExecuteAsync(request, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            logger.LogWarning("Job {JobName} ({RunId}) failed at dispatch: {Reason}", request.JobName, request.RunId, JobErrorSanitizer.Sanitize(error));
        }
        finally
        {
            _semaphore.Release();
        }
    }
}