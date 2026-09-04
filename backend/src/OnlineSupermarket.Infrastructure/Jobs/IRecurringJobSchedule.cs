namespace OnlineSupermarket.Infrastructure.Jobs;

public sealed record RecurringJobRequest(string JobName, string LockKey);

public interface IRecurringJobSchedule
{
    Task<IReadOnlyList<RecurringJobRequest>> GetDueJobsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken);
}