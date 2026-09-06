using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Jobs;

public sealed class JobRunStore(AppDbContext dbContext, TimeProvider timeProvider) : IJobRunStore
{
    private const int MaxErrorSummaryLength = 1000;

    public async Task<bool> TryClaimAsync(Guid runId, string token, DateTime leaseExpiresAtUtc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token cannot be empty", nameof(token));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (dbContext.Database.IsRelational())
        {
            return await dbContext.BackgroundJobRuns
                .Where(x => x.Id == runId && x.Status == JobRunStatus.Queued)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, JobRunStatus.Running)
                    .SetProperty(x => x.LockToken, token)
                    .SetProperty(x => x.StartedAtUtc, now)
                    .SetProperty(x => x.LeaseExpiresAtUtc, leaseExpiresAtUtc), cancellationToken) == 1;
        }

        var run = await dbContext.BackgroundJobRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);
        if (run is null || run.Status != JobRunStatus.Queued) return false;
        run.Start(token, now, leaseExpiresAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryRenewAsync(Guid runId, string token, DateTime newLeaseExpiresAtUtc, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (dbContext.Database.IsRelational())
        {
            return await dbContext.BackgroundJobRuns
                .Where(x => x.Id == runId
                            && x.Status == JobRunStatus.Running
                            && x.LockToken == token
                            && x.LeaseExpiresAtUtc >= now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.LeaseExpiresAtUtc, newLeaseExpiresAtUtc), cancellationToken) == 1;
        }

        var run = await dbContext.BackgroundJobRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);
        if (run is null || run.Status != JobRunStatus.Running || run.LockToken != token
            || (run.LeaseExpiresAtUtc.HasValue && run.LeaseExpiresAtUtc < now))
        {
            return false;
        }
        run.RenewLease(token, newLeaseExpiresAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryCompleteAsync(
        Guid runId,
        string token,
        bool success,
        string? error,
        DateTime completedAtUtc,
        CancellationToken cancellationToken)
    {
        var errorData = success ? null : Truncate(error, MaxErrorSummaryLength);
        if (dbContext.Database.IsRelational())
        {
            return await dbContext.BackgroundJobRuns
                .Where(x => x.Id == runId && x.Status == JobRunStatus.Running && x.LockToken == token)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, success ? JobRunStatus.Succeeded : JobRunStatus.Failed)
                    .SetProperty(x => x.CompletedAtUtc, completedAtUtc)
                    .SetProperty(x => x.ErrorSummary, errorData)
                    .SetProperty(x => x.LockToken, (string?)null)
                    .SetProperty(x => x.LeaseExpiresAtUtc, (DateTime?)null)
                    .SetProperty(x => x.LockKey, x => "released:" + x.Id), cancellationToken) == 1;
        }

        var run = await dbContext.BackgroundJobRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);
        if (run is null || run.Status != JobRunStatus.Running || run.LockToken != token) return false;
        if (success) run.MarkAsSucceeded(token, completedAtUtc);
        else run.MarkAsFailed(token, completedAtUtc, errorData ?? string.Empty);
        run.ClearLeaseOwnership();
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> FailExpiredAsync(DateTime nowUtc, string error, CancellationToken cancellationToken)
    {
        var errorData = Truncate(error, MaxErrorSummaryLength);
        if (dbContext.Database.IsRelational())
        {
            return await dbContext.BackgroundJobRuns
                .Where(x => x.Status == JobRunStatus.Running && x.LeaseExpiresAtUtc < nowUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, JobRunStatus.Failed)
                    .SetProperty(x => x.CompletedAtUtc, nowUtc)
                    .SetProperty(x => x.ErrorSummary, errorData)
                    .SetProperty(x => x.LockToken, (string?)null)
                    .SetProperty(x => x.LeaseExpiresAtUtc, (DateTime?)null)
                    .SetProperty(x => x.LockKey, x => "released:" + x.Id), cancellationToken);
        }

        var stale = await dbContext.BackgroundJobRuns
            .Where(x => x.Status == JobRunStatus.Running && x.LeaseExpiresAtUtc < nowUtc)
            .ToListAsync(cancellationToken);
        foreach (var run in stale)
        {
            run.MarkAsFailed(run.LockToken!, nowUtc, errorData);
            run.ClearLeaseOwnership();
        }
        if (stale.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= maxLength) return value;
        return value[..(maxLength - 3)] + "...";
    }
}