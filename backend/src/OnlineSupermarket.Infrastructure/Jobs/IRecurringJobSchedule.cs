namespace OnlineSupermarket.Infrastructure.Jobs;

public sealed record RecurringJobRequest(string JobName, string LockKey, Guid? BranchId = null);

public interface IRecurringJobSchedule
{
    Task<IReadOnlyList<RecurringJobRequest>> GetDueJobsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken);
}