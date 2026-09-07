using Microsoft.EntityFrameworkCore;
using Moq;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;
using Xunit;

namespace OnlineSupermarket.Infrastructure.Tests.Jobs;

public class JobRunCoordinatorTests
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<IJobQueue> _jobQueueMock;
    private readonly JobRunCoordinator _sut;

    public JobRunCoordinatorTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
            
        _dbContext = new AppDbContext(options);
        _jobQueueMock = new Mock<IJobQueue>();
        _sut = new JobRunCoordinator(_dbContext, _jobQueueMock.Object);
    }

    [Fact]
    public async Task TryQueueAsync_WhenNoConflict_ShouldQueueAndReturnRunId()
    {
        var result = await _sut.TryQueueAsync("TestJob", "Key1", CancellationToken.None);
        
        Assert.NotNull(result);
        var dbRow = await _dbContext.BackgroundJobRuns.SingleAsync();
        Assert.Equal(dbRow.Id, result);
        Assert.Equal("TestJob", dbRow.JobName);
        Assert.Equal(JobRunStatus.Queued, dbRow.Status);
        
        _jobQueueMock.Verify(q => q.EnqueueAsync(It.Is<JobRequest>(req => req.RunId == dbRow.Id && req.JobName == "TestJob"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryQueueAsync_WhenConflict_ShouldReturnNull()
    {
        var existing = new BackgroundJobRun("TestJob", "Key1", DateTime.UtcNow);
        _dbContext.BackgroundJobRuns.Add(existing);
        await _dbContext.SaveChangesAsync();
        
        var result = await _sut.TryQueueAsync("TestJob", "Key1", CancellationToken.None);
        
        Assert.Null(result);
        _jobQueueMock.Verify(q => q.EnqueueAsync(It.IsAny<JobRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryQueueAsync_WhenChannelFails_ShouldKeepQueuedRowAndReturnTrue()
    {
        _jobQueueMock.Setup(q => q.EnqueueAsync(It.IsAny<JobRequest>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Channel closed"));

        var result = await _sut.TryQueueAsync("TestJob", "Key2", CancellationToken.None);
        
        Assert.NotNull(result);
        
        var dbRow = await _dbContext.BackgroundJobRuns.SingleAsync(r => r.LockKey == "Key2");
        Assert.Equal("TestJob", dbRow.JobName);
        Assert.Equal(JobRunStatus.Queued, dbRow.Status);
    }
}
