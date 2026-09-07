using OnlineSupermarket.Domain.Jobs;
using Xunit;

namespace OnlineSupermarket.Domain.Tests.Jobs;

public class BackgroundJobRunTests
{
    private readonly DateTime _now = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);
    private readonly string _token = "token-123";

    [Fact]
    public void Constructor_ShouldInitializeAsQueued()
    {
        var run = new BackgroundJobRun("TestJob", "Key1", _now);
        
        Assert.Equal("TestJob", run.JobName);
        Assert.Equal("Key1", run.LockKey);
        Assert.Equal(JobRunStatus.Queued, run.Status);
        Assert.Equal(_now, run.CreatedAtUtc);
    }

    [Fact]
    public void Start_FromQueued_ShouldTransitionToRunning()
    {
        var run = new BackgroundJobRun("TestJob", "Key1", _now);
        var lease = _now.AddMinutes(5);
        
        run.Start(_token, _now.AddSeconds(1), lease);
        
        Assert.Equal(JobRunStatus.Running, run.Status);
        Assert.Equal(_token, run.LockToken);
        Assert.Equal(_now.AddSeconds(1), run.StartedAtUtc);
        Assert.Equal(lease, run.LeaseExpiresAtUtc);
    }

    [Fact]
    public void Start_FromRunning_ShouldThrow()
    {
        var run = new BackgroundJobRun("TestJob", "Key1", _now);
        run.Start(_token, _now, _now.AddMinutes(5));
        
        var act = () => run.Start("another-token", _now, _now.AddMinutes(5));
        
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void MarkAsSucceeded_WithValidToken_ShouldTransitionToSucceeded()
    {
        var run = new BackgroundJobRun("TestJob", "Key1", _now);
        run.Start(_token, _now, _now.AddMinutes(5));
        
        run.MarkAsSucceeded(_token, _now.AddMinutes(1));
        
        Assert.Equal(JobRunStatus.Succeeded, run.Status);
        Assert.Equal(_now.AddMinutes(1), run.CompletedAtUtc);
    }

    [Fact]
    public void MarkAsSucceeded_ShouldReleaseTheLockKeySlot()
    {
        var run = new BackgroundJobRun("TestJob", "Key1", _now);
        run.Start(_token, _now, _now.AddMinutes(5));

        run.MarkAsSucceeded(_token, _now.AddMinutes(1));

        Assert.StartsWith("released:", run.LockKey);
        Assert.NotEqual("Key1", run.LockKey);
    }

    [Fact]
    public void MarkAsFailed_ShouldReleaseTheLockKeySlot()
    {
        var run = new BackgroundJobRun("TestJob", "Key1", _now);
        run.Start(_token, _now, _now.AddMinutes(5));

        run.MarkAsFailed(_token, _now.AddMinutes(1), "Error occurred");

        Assert.StartsWith("released:", run.LockKey);
        Assert.Equal("Error occurred", run.ErrorSummary);
    }

    [Fact]
    public void MarkAsSucceeded_WithInvalidToken_ShouldThrow()
    {
        var run = new BackgroundJobRun("TestJob", "Key1", _now);
        run.Start(_token, _now, _now.AddMinutes(5));
        
        var act = () => run.MarkAsSucceeded("wrong-token", _now.AddMinutes(1));
        
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void MarkAsFailed_WithValidToken_ShouldTransitionToFailed()
    {
        var run = new BackgroundJobRun("TestJob", "Key1", _now);
        run.Start(_token, _now, _now.AddMinutes(5));
        
        run.MarkAsFailed(_token, _now.AddMinutes(1), "Error occurred");
        
        Assert.Equal(JobRunStatus.Failed, run.Status);
        Assert.Equal(_now.AddMinutes(1), run.CompletedAtUtc);
        Assert.Equal("Error occurred", run.ErrorSummary);
    }

    [Fact]
    public void MarkAsFailed_WithInvalidToken_ShouldThrow()
    {
        var run = new BackgroundJobRun("TestJob", "Key1", _now);
        run.Start(_token, _now, _now.AddMinutes(5));
        
        var act = () => run.MarkAsFailed("wrong-token", _now.AddMinutes(1), "Error");
        
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void RenewLease_WithValidToken_ShouldUpdateLease()
    {
        var run = new BackgroundJobRun("TestJob", "Key1", _now);
        run.Start(_token, _now, _now.AddMinutes(5));
        
        var newLease = _now.AddMinutes(10);
        run.RenewLease(_token, newLease);
        
        Assert.Equal(newLease, run.LeaseExpiresAtUtc);
    }
}
