using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Recommendations;

public class RecommendationRecurringSchedule(
    AppDbContext dbContext,
    IOptions<IntelligenceJobsOptions> options) : IRecurringJobSchedule
{
    private const string RecommendationsJobName = "Recommendations";
    private const string GlobalLockKey = "global";

    public async Task<IReadOnlyList<RecurringJobRequest>> GetDueJobsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var hasActiveRun = await dbContext.BackgroundJobRuns.AsNoTracking()
            .AnyAsync(run =>
                run.JobName == RecommendationsJobName
                && (run.Status == JobRunStatus.Queued || run.Status == JobRunStatus.Running),
                cancellationToken);

        if (hasActiveRun)
        {
            return [];
        }

        var latestRun = await dbContext.BackgroundJobRuns.AsNoTracking()
            .Where(run => run.JobName == RecommendationsJobName)
            .OrderByDescending(run => run.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestRun == null)
        {
            return [new RecurringJobRequest(RecommendationsJobName, GlobalLockKey)];
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.RecommendationIntervalMinutes));
        if (nowUtc - latestRun.CreatedAtUtc < interval)
        {
            return [];
        }

        return [new RecurringJobRequest(RecommendationsJobName, GlobalLockKey)];
    }
}