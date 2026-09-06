namespace OnlineSupermarket.Infrastructure.Jobs;

public interface IJobRunStore
{
    Task<bool> TryClaimAsync(
        Guid runId,
        string token,
        DateTime leaseExpiresAtUtc,
        CancellationToken cancellationToken);

    Task<bool> TryRenewAsync(
        Guid runId,
        string token,
        DateTime newLeaseExpiresAtUtc,
        CancellationToken cancellationToken);

    Task<bool> TryCompleteAsync(
        Guid runId,
        string token,
        bool success,
        string? error,
        DateTime completedAtUtc,
        CancellationToken cancellationToken);

    Task<int> FailExpiredAsync(
        DateTime nowUtc,
        string error,
        CancellationToken cancellationToken);
}