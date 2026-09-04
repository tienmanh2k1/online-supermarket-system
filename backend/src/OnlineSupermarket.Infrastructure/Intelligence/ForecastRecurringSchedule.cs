using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Intelligence;

public class ForecastRecurringSchedule(
    AppDbContext dbContext,
    IOptions<IntelligenceJobsOptions> options) : IRecurringJobSchedule
{
    private const string ForecastJobName = "Forecast";
    private const string BranchLockPrefix = "branch:";

    public async Task<IReadOnlyList<RecurringJobRequest>> GetDueJobsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(nowUtc);
        var dueTimeUtc = today.ToDateTime(new TimeOnly(options.Value.ForecastHourUtc, 0), DateTimeKind.Utc);
        if (nowUtc < dueTimeUtc)
        {
            return [];
        }

        var activeBranches = await dbContext.Branches.AsNoTracking()
            .Where(branch => branch.IsActive)
            .ToListAsync(cancellationToken);

        if (activeBranches.Count == 0)
        {
            return [];
        }

        var activeLocks = await dbContext.BackgroundJobRuns.AsNoTracking()
            .Where(run => run.JobName == ForecastJobName
                && (run.Status == JobRunStatus.Queued || run.Status == JobRunStatus.Running))
            .Select(run => run.LockKey)
            .ToListAsync(cancellationToken);
        var activeLockSet = activeLocks.ToHashSet();

        var alreadyForecastedToday = new HashSet<Guid>();
        var startOfTodayUtc = DateOnly.FromDateTime(nowUtc).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var succeededRunIds = await dbContext.BackgroundJobRuns.AsNoTracking()
            .Where(run => run.JobName == ForecastJobName
                && run.Status == JobRunStatus.Succeeded
                && run.CreatedAtUtc >= startOfTodayUtc)
            .Select(run => run.Id)
            .ToListAsync(cancellationToken);

        if (succeededRunIds.Count > 0)
        {
            var succeededInventoryIds = await dbContext.DemandForecasts.AsNoTracking()
                .Where(forecast => succeededRunIds.Contains(forecast.JobRunId))
                .Select(forecast => forecast.BranchInventoryId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (succeededInventoryIds.Count > 0)
            {
                var forecastedBranchIds = await dbContext.BranchInventories.AsNoTracking()
                    .Where(inventory => succeededInventoryIds.Contains(inventory.Id))
                    .Select(inventory => inventory.BranchId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                foreach (var branchId in forecastedBranchIds)
                {
                    alreadyForecastedToday.Add(branchId);
                }
            }
        }

        var dueJobs = new List<RecurringJobRequest>();
        foreach (var branch in activeBranches)
        {
            var lockKey = BranchLockPrefix + branch.Id;
            if (!activeLockSet.Contains(lockKey) && !alreadyForecastedToday.Contains(branch.Id))
            {
                dueJobs.Add(new RecurringJobRequest(ForecastJobName, lockKey));
            }
        }

        return dueJobs;
    }
}