using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Jobs;

/// <summary>
/// Guards the handler-side result publish: the run must still be Running with a
/// live lease inside the same transaction that inserts the result rows. On a
/// relational database a row lock on the run serializes with a concurrent
/// recovery failure, so a stale holder can never publish results for a terminal
/// run. If ownership is gone the publish is aborted.
/// </summary>
public static class JobRunPublishGuard
{
    public static async Task EnsureOwnedAsync(
        AppDbContext dbContext,
        Guid runId,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var provider = dbContext.Database.ProviderName ?? string.Empty;
        var isMySql = provider.Contains("MySql", StringComparison.OrdinalIgnoreCase);
        if (dbContext.Database.IsRelational() && isMySql)
        {
            var owns = await dbContext.BackgroundJobRuns
                .FromSqlInterpolated($"SELECT id, status, lease_expires_at_utc FROM background_job_runs WHERE id = {runId} FOR UPDATE")
                .AsNoTracking()
                .AnyAsync(x => x.Status == JobRunStatus.Running
                    && (x.LeaseExpiresAtUtc == null || x.LeaseExpiresAtUtc > now), cancellationToken);
            if (!owns)
                throw new OperationCanceledException("Job run is no longer owned; result publish aborted.");
            return;
        }

        var owned = await dbContext.BackgroundJobRuns.AnyAsync(
            x => x.Id == runId
                && x.Status == JobRunStatus.Running
                && (x.LeaseExpiresAtUtc == null || x.LeaseExpiresAtUtc > now),
            cancellationToken);
        if (!owned)
            throw new OperationCanceledException("Job run is no longer owned; result publish aborted.");
    }
}