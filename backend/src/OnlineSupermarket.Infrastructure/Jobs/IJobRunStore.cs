namespace OnlineSupermarket.Infrastructure.Jobs;

public interface IJobRunStore
{
    Task<bool> TryStartAsync(
        Guid runId,
        string token,
        DateTime leaseExpiresAtUtc,
        CancellationToken cancellationToken);

    Task<bool> TryRenewAsync(
        Guid runId,
        string token,
        DateTime newLeaseExpiresAtUtc,
        CancellationToken cancellationToken);

    Task<bool> TrySucceedAsync(
        Guid runId,
        string token,
        DateTime completedAtUtc,
        CancellationToken cancellationToken);

    Task<bool> TryFailAsync(
        Guid runId,
        string token,
        string? sanitizedError,
        DateTime completedAtUtc,
        CancellationToken cancellationToken);

    Task<int> FailExpiredAsync(
        DateTime nowUtc,
        string error,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<JobRequest>> GetQueuedRequestsAsync(CancellationToken cancellationToken);
}