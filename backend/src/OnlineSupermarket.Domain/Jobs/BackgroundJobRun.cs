using OnlineSupermarket.Domain.Common;

namespace OnlineSupermarket.Domain.Jobs;

public class BackgroundJobRun : Entity
{
    public string JobName { get; private set; }
    public string LockKey { get; private set; }
    public Guid? BranchId { get; private set; }
    public JobRunStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? ErrorSummary { get; private set; }
    public string? LockToken { get; private set; }
    public DateTime? LeaseExpiresAtUtc { get; private set; }

    private BackgroundJobRun() 
    { 
        JobName = string.Empty;
        LockKey = string.Empty;
    }

    public BackgroundJobRun(string jobName, string lockKey, DateTime createdAtUtc, Guid? branchId = null) 
        : base(Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(jobName)) throw new ArgumentException("Job name cannot be empty", nameof(jobName));
        if (string.IsNullOrWhiteSpace(lockKey)) throw new ArgumentException("Lock key cannot be empty", nameof(lockKey));
        
        JobName = jobName;
        LockKey = lockKey;
        BranchId = branchId;
        Status = JobRunStatus.Queued;
        CreatedAtUtc = createdAtUtc;
    }

    public void Start(string token, DateTime startedAtUtc, DateTime leaseExpiresAtUtc)
    {
        if (Status != JobRunStatus.Queued)
        {
            throw new InvalidOperationException($"Cannot start job from status {Status}");
        }

        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Token cannot be empty", nameof(token));

        Status = JobRunStatus.Running;
        LockToken = token;
        StartedAtUtc = startedAtUtc;
        LeaseExpiresAtUtc = leaseExpiresAtUtc;
    }

    public void RenewLease(string token, DateTime leaseExpiresAtUtc)
    {
        if (Status != JobRunStatus.Running)
        {
            throw new InvalidOperationException($"Cannot renew lease from status {Status}");
        }

        if (LockToken != token)
        {
            throw new InvalidOperationException("Invalid lock token");
        }

        LeaseExpiresAtUtc = leaseExpiresAtUtc;
    }

    public void MarkAsSucceeded(string token, DateTime completedAtUtc)
    {
        if (Status != JobRunStatus.Running)
        {
            throw new InvalidOperationException($"Cannot succeed job from status {Status}");
        }

        if (LockToken != token)
        {
            throw new InvalidOperationException("Invalid lock token");
        }

        Status = JobRunStatus.Succeeded;
        CompletedAtUtc = completedAtUtc;
        ReleaseLockSlot();
    }

    public void MarkAsFailed(string token, DateTime completedAtUtc, string error)
    {
        if (Status != JobRunStatus.Running)
        {
            throw new InvalidOperationException($"Cannot fail job from status {Status}");
        }

        if (LockToken != token)
        {
            throw new InvalidOperationException("Invalid lock token");
        }

        Status = JobRunStatus.Failed;
        CompletedAtUtc = completedAtUtc;
        ErrorSummary = error;
        ReleaseLockSlot();
    }

    // Terminal rows must not hold the (JobName, LockKey) unique slot, otherwise a
    // recurring job can never be queued twice. The released key stays non-null and unique.
    private void ReleaseLockSlot()
    {
        if (!LockKey.StartsWith("released:"))
        {
            LockKey = $"released:{Id}";
        }
    }
}
