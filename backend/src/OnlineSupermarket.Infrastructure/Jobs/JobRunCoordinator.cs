using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Jobs;

public class JobRunCoordinator(AppDbContext dbContext, IJobQueue jobQueue)
{
    public async Task<Guid?> TryQueueAsync(
        string jobName, string lockKey, CancellationToken cancellationToken, Guid? branchId = null)
    {
        var exists = await dbContext.BackgroundJobRuns.AnyAsync(
            run => run.JobName == jobName && run.LockKey == lockKey, cancellationToken);
        if (exists)
        {
            return null;
        }

        var run = new BackgroundJobRun(jobName, lockKey, DateTime.UtcNow, branchId);
        dbContext.BackgroundJobRuns.Add(run);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error)
        {
            if (!IsDuplicateKeyError(error))
            {
                throw;
            }

            return null;
        }

        try
        {
            await jobQueue.EnqueueAsync(new JobRequest(run.Id, jobName), cancellationToken);
        }
        catch
        {
            // Ignore channel errors to ensure durable DB persistence (job will be picked up by recovery)
        }

        return run.Id;
    }

    private static bool IsDuplicateKeyError(DbUpdateException error)
    {
        Exception? current = error;
        var seen = new HashSet<int>();
        while (current != null && seen.Add(current.GetHashCode()))
        {
            var message = current.Message ?? string.Empty;
            if (message.Contains("1062")
                || message.Contains("duplicate key")
                || message.Contains("Duplicate entry")
                || message.Contains("UNIQUE constraint failed"))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}