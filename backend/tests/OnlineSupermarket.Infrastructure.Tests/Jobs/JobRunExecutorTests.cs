using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;
using Xunit;

namespace OnlineSupermarket.Infrastructure.Tests.Jobs;

public sealed class JobRunExecutorTests
{
    private sealed class TestLoggerProvider(List<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new TestLogger(logs);
        public void Dispose() { }
    }

    private sealed class TestLogger(List<string> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = formatter(state, exception);
            if (exception != null)
                msg += " " + exception.ToString();
            logs.Add(msg);
        }
    }

    private sealed class RecordingHandler(string jobName) : IBackgroundJobHandler
    {
        public string JobName => jobName;
        public int Invocations { get; private set; }
        public Exception? ToThrow { get; set; }
        public Func<CancellationToken, Task>? Gate { get; set; }
        public CancellationToken ReceivedToken { get; private set; } = CancellationToken.None;

        public async Task HandleAsync(Guid runId, CancellationToken cancellationToken)
        {
            Invocations++;
            ReceivedToken = cancellationToken;
            if (Gate is not null)
                await Gate(cancellationToken);
            if (ToThrow is not null)
                throw ToThrow;
        }
    }

    private sealed class Harness
    {
        public string DbName { get; }
        public Dictionary<string, RecordingHandler> Handlers { get; } = new();
        public ServiceProvider Services { get; }
        public List<string> LoggedMessages { get; } = [];

        public Harness(string dbName, IJobRunStore? storeOverride, params RecordingHandler[] handlers)
        {
            DbName = dbName;
            foreach (var handler in handlers)
                Handlers[handler.JobName] = handler;

            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(dbName));
            services.AddDbContextFactory<AppDbContext>(opts => opts.UseInMemoryDatabase(dbName));
            services.AddLogging(builder =>
            {
                builder.AddProvider(new TestLoggerProvider(LoggedMessages));
            });
            services.AddSingleton(TimeProvider.System);
            if (storeOverride is not null)
                services.AddScoped<IJobRunStore>(_ => storeOverride);
            else
                services.AddScoped<IJobRunStore, EfJobRunStore>();
            foreach (var handler in handlers)
                services.AddSingleton<IBackgroundJobHandler>(handler);
            services.AddScoped<JobRunExecutor>();
            Services = services.BuildServiceProvider();
        }

        public JobRunExecutor CreateExecutor()
        {
            using var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<JobRunExecutor>();
        }

        public async Task<BackgroundJobRun> SeedAsync(
            JobRunStatus status = JobRunStatus.Queued,
            string jobName = "JobA",
            string lockKey = "Key-1",
            string? token = null)
        {
            await using var db = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(DbName).Options);
            var run = new BackgroundJobRun(jobName, lockKey, DateTime.UtcNow.AddHours(-1));
            if (status == JobRunStatus.Running)
            {
                run.Start(token ?? "existing-token", DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
            }
            db.BackgroundJobRuns.Add(run);
            await db.SaveChangesAsync();
            return run;
        }

        public async Task<BackgroundJobRun> ReloadAsync(Guid runId)
        {
            await using var db = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(DbName).Options);
            return await db.BackgroundJobRuns.SingleAsync(x => x.Id == runId);
        }
    }

    private sealed class FailRenewStore(IJobRunStore inner) : IJobRunStore
    {
        public int RenewCalls { get; private set; }

        public Task<bool> TryStartAsync(Guid runId, string token, DateTime leaseExpiresAtUtc, CancellationToken cancellationToken)
            => inner.TryStartAsync(runId, token, leaseExpiresAtUtc, cancellationToken);

        public Task<bool> TryRenewAsync(Guid runId, string token, DateTime newLeaseExpiresAtUtc, CancellationToken cancellationToken)
        {
            RenewCalls++;
            return Task.FromResult(false);
        }

        public Task<bool> TrySucceedAsync(Guid runId, string token, DateTime completedAtUtc, CancellationToken cancellationToken)
            => inner.TrySucceedAsync(runId, token, completedAtUtc, cancellationToken);

        public Task<bool> TryFailAsync(Guid runId, string token, string? sanitizedError, DateTime completedAtUtc, CancellationToken cancellationToken)
            => inner.TryFailAsync(runId, token, sanitizedError, completedAtUtc, cancellationToken);

        public Task<int> FailExpiredAsync(DateTime nowUtc, string error, CancellationToken cancellationToken)
            => inner.FailExpiredAsync(nowUtc, error, cancellationToken);

        public Task<IReadOnlyList<JobRequest>> GetQueuedRequestsAsync(CancellationToken cancellationToken)
            => inner.GetQueuedRequestsAsync(cancellationToken);
    }

    [Fact]
    public async Task SuccessfulHandler_SucceedsRun_WithReleasedLock()
    {
        var handler = new RecordingHandler("JobA");
        var harness = new Harness(Guid.NewGuid().ToString(), null, handler);
        var run = await harness.SeedAsync();

        var executor = harness.CreateExecutor();
        await executor.ExecuteAsync(new JobRequest(run.Id, "JobA"), CancellationToken.None);

        Assert.Equal(1, handler.Invocations);
        var reloaded = await harness.ReloadAsync(run.Id);
        Assert.Equal(JobRunStatus.Succeeded, reloaded.Status);
        Assert.Equal($"released:{run.Id}", reloaded.LockKey);
        Assert.Null(reloaded.LockToken);
        Assert.Null(reloaded.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task ThrowingHandler_FailsRun_WithSanitizedError()
    {
        var handler = new RecordingHandler("JobA");
        handler.ToThrow = new InvalidOperationException("Handler exploded");
        var harness = new Harness(Guid.NewGuid().ToString(), null, handler);
        var run = await harness.SeedAsync();

        var executor = harness.CreateExecutor();
        await executor.ExecuteAsync(new JobRequest(run.Id, "JobA"), CancellationToken.None);

        var reloaded = await harness.ReloadAsync(run.Id);
        Assert.Equal(JobRunStatus.Failed, reloaded.Status);
        Assert.Contains("Handler exploded", reloaded.ErrorSummary);
        Assert.Equal($"released:{run.Id}", reloaded.LockKey);
        Assert.Null(reloaded.LockToken);
    }

    [Fact]
    public async Task ThrowingHandler_RedactsSyntheticSecretMarker_FromErrorSummaryAndLogs()
    {
        var handler = new RecordingHandler("JobA");
        handler.ToThrow = new InvalidOperationException("Failed with credentials: {\"password\":\"prefix\\\"AUDIT_SYNTHETIC_MARKER\",\"secret\":'inner\\'SYNTHETIC_KEY_MARKER'}");
        var harness = new Harness(Guid.NewGuid().ToString(), null, handler);
        var run = await harness.SeedAsync();

        var executor = harness.CreateExecutor();
        await executor.ExecuteAsync(new JobRequest(run.Id, "JobA"), CancellationToken.None);

        var reloaded = await harness.ReloadAsync(run.Id);
        Assert.Equal(JobRunStatus.Failed, reloaded.Status);
        Assert.DoesNotContain("AUDIT_SYNTHETIC_MARKER", reloaded.ErrorSummary);
        Assert.DoesNotContain("SYNTHETIC_KEY_MARKER", reloaded.ErrorSummary);
        Assert.Contains("[REDACTED]", reloaded.ErrorSummary);

        Assert.NotEmpty(harness.LoggedMessages);
        foreach (var msg in harness.LoggedMessages)
        {
            Assert.DoesNotContain("AUDIT_SYNTHETIC_MARKER", msg);
            Assert.DoesNotContain("SYNTHETIC_KEY_MARKER", msg);
        }
    }

    [Fact]
    public async Task UnknownHandler_FailsRun_WithoutInvokingAnyHandler()
    {
        var known = new RecordingHandler("KnownJob");
        var harness = new Harness(Guid.NewGuid().ToString(), null, known);
        var run = await harness.SeedAsync(jobName: "UnknownJob");

        var executor = harness.CreateExecutor();
        await executor.ExecuteAsync(new JobRequest(run.Id, "UnknownJob"), CancellationToken.None);

        Assert.Equal(0, known.Invocations);
        var reloaded = await harness.ReloadAsync(run.Id);
        Assert.Equal(JobRunStatus.Failed, reloaded.Status);
        Assert.Contains("No handler registered", reloaded.ErrorSummary);
    }

    [Fact]
    public async Task DuplicateRequest_AlreadyRunning_IsSkipped()
    {
        var handler = new RecordingHandler("JobA");
        var harness = new Harness(Guid.NewGuid().ToString(), null, handler);
        var run = await harness.SeedAsync(JobRunStatus.Running, "JobA", "Key-1", "other-token");

        var executor = harness.CreateExecutor();
        await executor.ExecuteAsync(new JobRequest(run.Id, "JobA"), CancellationToken.None);

        Assert.Equal(0, handler.Invocations);
        var reloaded = await harness.ReloadAsync(run.Id);
        Assert.Equal(JobRunStatus.Running, reloaded.Status);
        Assert.Equal("other-token", reloaded.LockToken);
    }

    [Fact]
    public async Task HostCancellation_LeavesRunRunningForRecovery()
    {
        var handler = new RecordingHandler("JobA");
        handler.Gate = ct => Task.Delay(Timeout.InfiniteTimeSpan, ct);
        var harness = new Harness(Guid.NewGuid().ToString(), null, handler);
        var run = await harness.SeedAsync();

        var executor = harness.CreateExecutor();
        using var cts = new CancellationTokenSource();
        var task = executor.ExecuteAsync(new JobRequest(run.Id, "JobA"), cts.Token);
        await Task.Delay(300);
        var handlerToken = handler.ReceivedToken;
        await cts.CancelAsync();
        await task;

        Assert.True(handlerToken.IsCancellationRequested);
        var reloaded = await harness.ReloadAsync(run.Id);
        Assert.Equal(JobRunStatus.Running, reloaded.Status);
    }

    [Fact]
    public async Task HeartbeatLoss_CancelsHandler_AndLeavesRunForRecovery()
    {
        var runStoreName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(runStoreName).Options;
        await using var seed = new AppDbContext(options);
        var run = new BackgroundJobRun("JobA", "Key-heavy", DateTime.UtcNow.AddHours(-1));
        seed.BackgroundJobRuns.Add(run);
        await seed.SaveChangesAsync();

        var inner = new EfJobRunStore(new TestDbContextFactory(options));
        var failing = new FailRenewStore(inner);
        var handler = new RecordingHandler("JobA");
        handler.Gate = ct => Task.Delay(Timeout.InfiniteTimeSpan, ct);
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(runStoreName));
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IJobRunStore>(_ => failing);
        services.AddSingleton<IBackgroundJobHandler>(handler);
        services.AddScoped<JobRunExecutor>();
        using var provider = services.BuildServiceProvider();

        JobRunExecutor executor;
        using (var scope = provider.CreateScope())
            executor = scope.ServiceProvider.GetRequiredService<JobRunExecutor>();
        executor.HeartbeatIntervalOverride = TimeSpan.FromMilliseconds(50);

        await executor.ExecuteAsync(new JobRequest(run.Id, "JobA"), CancellationToken.None);

        Assert.True(failing.RenewCalls >= 1);
        Assert.True(handler.ReceivedToken.IsCancellationRequested);
        await using var verify = new AppDbContext(options);
        var row = await verify.BackgroundJobRuns.SingleAsync(x => x.Id == run.Id);
        // The abandoned run stays Running; startup recovery owns the terminal flip.
        Assert.Equal(JobRunStatus.Running, row.Status);
        Assert.NotNull(row.LockToken);
    }

    [Fact]
    public async Task TerminalRun_IsNeverReused()
    {
        var handler = new RecordingHandler("JobA");
        var harness = new Harness(Guid.NewGuid().ToString(), null, handler);
        var run = await harness.SeedAsync();
        await harness.ReloadAsync(run.Id); // sanity reload

        // First run completes.
        var executor = harness.CreateExecutor();
        await executor.ExecuteAsync(new JobRequest(run.Id, "JobA"), CancellationToken.None);

        // A duplicate dequeue after terminal must not re-invoke the handler.
        await executor.ExecuteAsync(new JobRequest(run.Id, "JobA"), CancellationToken.None);

        Assert.Equal(1, handler.Invocations);
        var reloaded = await harness.ReloadAsync(run.Id);
        Assert.Equal(JobRunStatus.Succeeded, reloaded.Status);
    }
}