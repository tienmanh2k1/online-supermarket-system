using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;
using Xunit;

namespace OnlineSupermarket.Infrastructure.Tests.Jobs;

public sealed class RecurringJobSchedulerTests
{
    private sealed class TestLoggerProvider(List<string> logs, TaskCompletionSource<bool> logSignal) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new TestLogger(logs, logSignal);
        public void Dispose() { }
    }

    private sealed class TestLogger(List<string> logs, TaskCompletionSource<bool> logSignal) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = formatter(state, exception);
            if (exception != null)
                msg += " " + exception.ToString();
            logs.Add(msg);
            if (msg.Contains("Recurring job scheduler iteration failed"))
            {
                logSignal.TrySetResult(true);
            }
        }
    }

    [Fact]
    public async Task Scheduler_IterationException_RedactsSyntheticSecretMarkerInLog()
    {
        var logs = new List<string>();
        var logSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();

        var storeMock = new Mock<IJobRunStore>();
        storeMock
            .Setup(s => s.GetQueuedRequestsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Recovery failure: {\"token\":\"prefix\\\"AUDIT_SYNTHETIC_MARKER\",\"password\":'inner\\'SYNTHETIC_PASS_MARKER'}"));

        var queueMock = new Mock<IJobQueue>();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(storeMock.Object);
        services.AddSingleton(queueMock.Object);
        services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<JobLeaseService>();
        services.AddScoped<JobRunCoordinator>();
        services.AddLogging(builder =>
        {
            builder.AddProvider(new TestLoggerProvider(logs, logSignal));
        });

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<RecurringJobScheduler>();

        var scheduler = new RecurringJobScheduler(scopeFactory, TimeProvider.System, logger);

        using var cts = new CancellationTokenSource();
        try
        {
            await scheduler.StartAsync(cts.Token);
            var completed = await Task.WhenAny(logSignal.Task, Task.Delay(5000)) == logSignal.Task;
            Assert.True(completed, "Scheduler should have logged iteration failure signal within timeout.");
            await cts.CancelAsync();
            if (scheduler.ExecuteTask is not null)
            {
                await scheduler.ExecuteTask;
            }
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
            scheduler.Dispose();
            provider.Dispose();
        }

        Assert.NotEmpty(logs);
        var failureLog = logs.FirstOrDefault(l => l.Contains("Recurring job scheduler iteration failed"));
        Assert.NotNull(failureLog);
        Assert.DoesNotContain("AUDIT_SYNTHETIC_MARKER", failureLog);
        Assert.DoesNotContain("SYNTHETIC_PASS_MARKER", failureLog);
        Assert.Contains("[REDACTED]", failureLog);
    }
}
