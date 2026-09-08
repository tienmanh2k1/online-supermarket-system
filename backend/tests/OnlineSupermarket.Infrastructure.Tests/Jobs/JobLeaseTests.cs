using Microsoft.EntityFrameworkCore;
using Moq;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;
using Xunit;

namespace OnlineSupermarket.Infrastructure.Tests.Jobs;

public class JobLeaseTests
{
    private readonly string _dbName;
    private readonly Mock<IJobQueue> _queueMock;
    private readonly JobLeaseService _sut;

    public JobLeaseTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        _queueMock = new Mock<IJobQueue>();
        var store = new EfJobRunStore(new TestDbContextFactory(options));
        _sut = new JobLeaseService(store, _queueMock.Object, TimeProvider.System);
    }

    private AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options);

    [Fact]
    public async Task RecoverStartupJobs_ShouldRequeueQueuedJobs()
    {
        var queuedRun = new BackgroundJobRun("TestJob1", "Key1", DateTime.UtcNow.AddHours(-1));
        await using (var db = CreateContext())
        {
            db.BackgroundJobRuns.Add(queuedRun);
            await db.SaveChangesAsync();
        }

        await _sut.RecoverStaleJobsAsync(CancellationToken.None);

        _queueMock.Verify(q => q.EnqueueAsync(It.Is<JobRequest>(r => r.RunId == queuedRun.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecoverStartupJobs_ShouldFailExpiredRunningJobsAtomically()
    {
        var staleRun = new BackgroundJobRun("TestJob2", "Key2", DateTime.UtcNow.AddHours(-2));
        staleRun.Start("token1", DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddMinutes(-30));
        var activeRun = new BackgroundJobRun("TestJob3", "Key3", DateTime.UtcNow.AddHours(-2));
        activeRun.Start("token2", DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddMinutes(30));
        await using (var db = CreateContext())
        {
            db.BackgroundJobRuns.AddRange(staleRun, activeRun);
            await db.SaveChangesAsync();
        }

        await _sut.RecoverStaleJobsAsync(CancellationToken.None);

        await using (var db = CreateContext())
        {
            var staleDb = await db.BackgroundJobRuns.SingleAsync(x => x.Id == staleRun.Id);
            Assert.Equal(JobRunStatus.Failed, staleDb.Status);
            Assert.Equal($"released:{staleRun.Id}", staleDb.LockKey);
            Assert.Null(staleDb.LockToken);

            var activeDb = await db.BackgroundJobRuns.SingleAsync(x => x.Id == activeRun.Id);
            Assert.Equal(JobRunStatus.Running, activeDb.Status);
        }
    }

    [Fact]
    public async Task RecoverStartupJobs_ShouldNotDoubleFailTerminalRuns()
    {
        var completedRun = new BackgroundJobRun("TestJob4", "Key4", DateTime.UtcNow.AddHours(-2));
        completedRun.Start("token3", DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddMinutes(-30));
        completedRun.MarkAsFailed("token3", DateTime.UtcNow.AddHours(-1), "already failed");
        completedRun.ClearLeaseOwnership();
        await using (var db = CreateContext())
        {
            db.BackgroundJobRuns.Add(completedRun);
            await db.SaveChangesAsync();
        }

        await _sut.RecoverStaleJobsAsync(CancellationToken.None);

        await using (var db = CreateContext())
        {
            var row = await db.BackgroundJobRuns.SingleAsync(x => x.Id == completedRun.Id);
            Assert.Equal(JobRunStatus.Failed, row.Status);
            Assert.Equal("already failed", row.ErrorSummary);
            Assert.Equal($"released:{completedRun.Id}", row.LockKey);
        }
    }
}