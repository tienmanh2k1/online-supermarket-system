using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Jobs;

public sealed class EfJobRunStore(IDbContextFactory<AppDbContext> dbContextFactory) : IJobRunStore
{
    private const int MaxErrorSummaryLength = 1000;

    public async Task<bool> TryStartAsync(
        Guid runId,
        string token,
        DateTime leaseExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token cannot be empty", nameof(token));

        var now = DateTime.UtcNow;
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (db.Database.IsRelational())
        {
            return await db.BackgroundJobRuns
                .Where(x => x.Id == runId && x.Status == JobRunStatus.Queued)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, JobRunStatus.Running)
                    .SetProperty(x => x.LockToken, token)
                    .SetProperty(x => x.StartedAtUtc, now)
                    .SetProperty(x => x.LeaseExpiresAtUtc, leaseExpiresAtUtc), cancellationToken) == 1;
        }

        var run = await db.BackgroundJobRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);
        if (run is null || run.Status != JobRunStatus.Queued) return false;
        run.Start(token, now, leaseExpiresAtUtc);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryRenewAsync(
        Guid runId,
        string token,
        DateTime newLeaseExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (db.Database.IsRelational())
        {
            return await db.BackgroundJobRuns
                .Where(x => x.Id == runId
                            && x.Status == JobRunStatus.Running
                            && x.LockToken == token
                            && x.LeaseExpiresAtUtc >= now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.LeaseExpiresAtUtc, newLeaseExpiresAtUtc), cancellationToken) == 1;
        }

        var run = await db.BackgroundJobRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);
        if (run is null || run.Status != JobRunStatus.Running || run.LockToken != token
            || (run.LeaseExpiresAtUtc.HasValue && run.LeaseExpiresAtUtc < now))
        {
            return false;
        }
        run.RenewLease(token, newLeaseExpiresAtUtc);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TrySucceedAsync(
        Guid runId,
        string token,
        DateTime completedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (db.Database.IsRelational())
        {
            return await TerminalTransitionAsync(db, runId, token, JobRunStatus.Succeeded, null, completedAtUtc, cancellationToken) == 1;
        }

        var run = await db.BackgroundJobRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);
        if (run is null || run.Status != JobRunStatus.Running || run.LockToken != token) return false;
        run.MarkAsSucceeded(token, completedAtUtc);
        run.ClearLeaseOwnership();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryFailAsync(
        Guid runId,
        string token,
        string? sanitizedError,
        DateTime completedAtUtc,
        CancellationToken cancellationToken)
    {
        var errorData = Truncate(sanitizedError, MaxErrorSummaryLength);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (db.Database.IsRelational())
        {
            return await TerminalTransitionAsync(db, runId, token, JobRunStatus.Failed, errorData, completedAtUtc, cancellationToken) == 1;
        }

        var run = await db.BackgroundJobRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);
        if (run is null || run.Status != JobRunStatus.Running || run.LockToken != token) return false;
        run.MarkAsFailed(token, completedAtUtc, errorData);
        run.ClearLeaseOwnership();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static async Task<int> TerminalTransitionAsync(
        AppDbContext db,
        Guid runId,
        string token,
        JobRunStatus terminalStatus,
        string? errorData,
        DateTime completedAtUtc,
        CancellationToken cancellationToken)
    {
        return await db.BackgroundJobRuns
            .Where(x => x.Id == runId && x.Status == JobRunStatus.Running && x.LockToken == token)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, terminalStatus)
                .SetProperty(x => x.CompletedAtUtc, completedAtUtc)
                .SetProperty(x => x.ErrorSummary, errorData)
                .SetProperty(x => x.LockToken, (string?)null)
                .SetProperty(x => x.LeaseExpiresAtUtc, (DateTime?)null)
                .SetProperty(x => x.LockKey, x => "released:" + x.Id), cancellationToken);
    }

    public async Task<int> FailExpiredAsync(DateTime nowUtc, string error, CancellationToken cancellationToken)
    {
        var errorData = Truncate(error, MaxErrorSummaryLength);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (db.Database.IsRelational())
        {
            return await db.BackgroundJobRuns
                .Where(x => x.Status == JobRunStatus.Running && x.LeaseExpiresAtUtc < nowUtc)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, JobRunStatus.Failed)
                    .SetProperty(x => x.CompletedAtUtc, nowUtc)
                    .SetProperty(x => x.ErrorSummary, errorData)
                    .SetProperty(x => x.LockToken, (string?)null)
                    .SetProperty(x => x.LeaseExpiresAtUtc, (DateTime?)null)
                    .SetProperty(x => x.LockKey, x => "released:" + x.Id), cancellationToken);
        }

        var stale = await db.BackgroundJobRuns
            .Where(x => x.Status == JobRunStatus.Running && x.LeaseExpiresAtUtc < nowUtc)
            .ToListAsync(cancellationToken);
        foreach (var run in stale)
        {
            run.MarkAsFailed(run.LockToken!, nowUtc, errorData);
            run.ClearLeaseOwnership();
        }
        if (stale.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
        return stale.Count;
    }

    public async Task<IReadOnlyList<JobRequest>> GetQueuedRequestsAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.BackgroundJobRuns
            .Where(x => x.Status == JobRunStatus.Queued)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new JobRequest(x.Id, x.JobName))
            .ToListAsync(cancellationToken);
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= maxLength) return value;
        return value[..(maxLength - 3)] + "...";
    }
}