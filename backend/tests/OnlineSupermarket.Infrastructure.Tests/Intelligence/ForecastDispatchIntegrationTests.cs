using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Tests.Intelligence;

public sealed class ForecastDispatchIntegrationTests
{
    [Fact]
    public async Task Coordinator_WithBranchId_SetsItOnTheQueuedRun()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new AppDbContext(options);
            db.Database.EnsureCreated();
            var coordinator = new JobRunCoordinator(db, new ChannelJobQueue());
            var branch = new Branch("Dispatch Branch", "1 Test Street", "0100000000", 10m, 106m);
            db.Branches.Add(branch);
            await db.SaveChangesAsync();
            var branchId = branch.Id;

            var runId = await coordinator.TryQueueAsync(
                "Forecast", "branch:" + branchId, CancellationToken.None, branchId);

            Assert.NotNull(runId);
            var run = await db.BackgroundJobRuns.SingleAsync(candidate => candidate.Id == runId);
            Assert.Equal(branchId, run.BranchId);
            Assert.Equal(JobRunStatus.Queued, run.Status);

            db.Dispose();
        }
        finally
        {
            connection.Dispose();
        }
    }
}