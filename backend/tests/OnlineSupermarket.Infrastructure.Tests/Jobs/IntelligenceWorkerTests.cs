using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;
using Xunit;

namespace OnlineSupermarket.Infrastructure.Tests.Jobs;

public class IntelligenceWorkerTests
{
    private readonly Mock<IJobQueue> _queueMock = new();
    private readonly Mock<IBackgroundJobHandler> _handlerMock = new();
    private readonly string _dbName = Guid.NewGuid().ToString();

    private DbContextOptions<AppDbContext> DbOptions =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;

    private async Task<BackgroundJobRun> SeedRunAsync(JobRunStatus status = JobRunStatus.Queued, string jobName = "KnownJob")
    {
        await using var db = new AppDbContext(DbOptions);
        var run = new BackgroundJobRun(jobName, $"Key-{Guid.NewGuid():N}", DateTime.UtcNow.AddHours(-1));
        if (status == JobRunStatus.Running)
        {
            run.Start("existing-token", DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
        }
        db.BackgroundJobRuns.Add(run);
        await db.SaveChangesAsync();
        return run;
    }

    private (IntelligenceWorker Worker, IServiceProvider Services) BuildWorker() =>
        BuildWorker(_handlerMock.Object);

    private (IntelligenceWorker Worker, IServiceProvider Services) BuildWorker(IBackgroundJobHandler handler)
    {
        var services = new ServiceCollection();
        services.AddScoped<AppDbContext>(_ => new AppDbContext(DbOptions));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IJobRunStore, JobRunStore>();
        services.AddSingleton(handler);
        var provider = services.BuildServiceProvider();

        var worker = new IntelligenceWorker(
            _queueMock.Object,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new IntelligenceJobsOptions()),
            NullLogger<IntelligenceWorker>.Instance);
        return (worker, provider);
    }

    private void EnqueueSequence(params JobRequest[] requests)
    {
        var sequence = new Queue<JobRequest>(requests);
        _queueMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (sequence.Count > 0)
                    return new ValueTask<JobRequest>(sequence.Dequeue());
                return new ValueTask<JobRequest>(Task.Delay(Timeout.InfiniteTimeSpan).ContinueWith(_ => (JobRequest)null!));
            });
    }

    [Fact]
    public async Task ClaimsRun_DispatchesHandler_AndCompletesSuccess()
    {
        var run = await SeedRunAsync();
        _handlerMock.SetupGet(h => h.JobName).Returns("KnownJob");
        _handlerMock.Setup(h => h.HandleAsync(run.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        EnqueueSequence(new JobRequest(run.Id, "KnownJob"));
        var (worker, _) = BuildWorker();

        using var cts = new CancellationTokenSource();
        var execute = worker.StartAsync(cts.Token);
        await Task.Delay(300);
        await cts.CancelAsync();
        await execute;

        _handlerMock.Verify(h => h.HandleAsync(run.Id, It.IsAny<CancellationToken>()), Times.Once);
        await using var db = new AppDbContext(DbOptions);
        var row = await db.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(JobRunStatus.Succeeded, row.Status);
        Assert.Equal($"released:{run.Id}", row.LockKey);
        Assert.Null(row.LockToken);
    }

    [Fact]
    public async Task WhenRunAlreadyClaimed_HandlerIsNotInvoked()
    {
        var run = await SeedRunAsync(JobRunStatus.Running);
        _handlerMock.SetupGet(h => h.JobName).Returns("KnownJob");
        EnqueueSequence(new JobRequest(run.Id, "KnownJob"));
        var (worker, _) = BuildWorker();

        using var cts = new CancellationTokenSource();
        var execute = worker.StartAsync(cts.Token);
        await Task.Delay(300);
        await cts.CancelAsync();
        await execute;

        _handlerMock.Verify(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        await using var db = new AppDbContext(DbOptions);
        var row = await db.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(JobRunStatus.Running, row.Status);
        Assert.Equal("existing-token", row.LockToken);
    }

    [Fact]
    public async Task HandlerException_FailsRunWithSanitizedError()
    {
        var run = await SeedRunAsync();
        _handlerMock.SetupGet(h => h.JobName).Returns("KnownJob");
        _handlerMock.Setup(h => h.HandleAsync(run.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Handler failed"));
        EnqueueSequence(new JobRequest(run.Id, "KnownJob"));
        var (worker, _) = BuildWorker();

        using var cts = new CancellationTokenSource();
        var execute = worker.StartAsync(cts.Token);
        await Task.Delay(300);
        await cts.CancelAsync();
        await execute;

        await using var db = new AppDbContext(DbOptions);
        var row = await db.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(JobRunStatus.Failed, row.Status);
        Assert.Contains("Handler failed", row.ErrorSummary);
        Assert.Equal($"released:{run.Id}", row.LockKey);
    }

    [Fact]
    public async Task UnknownHandler_FailsRunWithoutInvokingAnyHandler()
    {
        var run = await SeedRunAsync(jobName: "UnknownJob");
        _handlerMock.SetupGet(h => h.JobName).Returns("KnownJob");
        EnqueueSequence(new JobRequest(run.Id, "UnknownJob"));
        var (worker, _) = BuildWorker();

        using var cts = new CancellationTokenSource();
        var execute = worker.StartAsync(cts.Token);
        await Task.Delay(300);
        await cts.CancelAsync();
        await execute;

        _handlerMock.Verify(h => h.HandleAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        await using var db = new AppDbContext(DbOptions);
        var row = await db.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        Assert.Equal(JobRunStatus.Failed, row.Status);
        Assert.Contains("No handler registered", row.ErrorSummary);
    }
}