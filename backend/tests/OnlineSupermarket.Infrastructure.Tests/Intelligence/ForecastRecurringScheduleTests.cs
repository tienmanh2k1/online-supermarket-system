using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Intelligence;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Intelligence;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Tests.Intelligence;

public sealed class ForecastRecurringScheduleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly ForecastRecurringSchedule _schedule;
    private readonly ForecastJobHandler _handler;
    private Guid _categoryId;
    private Guid _brandId;
    private bool _catalogSeeded;

    public ForecastRecurringScheduleTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _schedule = new ForecastRecurringSchedule(
            _db, Options.Create(new IntelligenceJobsOptions { ForecastHourUtc = 1 }));
        _handler = new ForecastJobHandler(_db, TimeProvider.System);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static DateTime AtUtcHour(int hour)
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Year, now.Month, now.Day, hour, 0, 0, DateTimeKind.Utc);
    }

    private async Task<Guid> SeedBranchAsync(string name, bool active = true)
    {
        var branch = new Branch(name, "1 Test Street", "0100000000", 10m, 106m);
        if (!active)
        {
            branch.Deactivate();
        }

        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();
        return branch.Id;
    }

    private async Task<Guid> SeedInventoryAsync(Guid branchId)
    {
        if (!_catalogSeeded)
        {
            var category = new Category("ForecastScheduleCats", "forecast-schedule-cats");
            var brand = new Brand("ForecastScheduleBrand", "forecast-schedule-brand");
            _db.Categories.Add(category);
            _db.Brands.Add(brand);
            _categoryId = category.Id;
            _brandId = brand.Id;
            _catalogSeeded = true;
        }

        var product = new Product(_categoryId, _brandId,
            $"SKU-S-{Guid.NewGuid():N}".Substring(0, 32), "Schedule Product",
            $"schedule-product-{Guid.NewGuid():N}", "desc", 45_000m, "cái", null);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var inventory = BranchInventory.Create(branchId, product.Id, 45_000m, 100, 5);
        _db.BranchInventories.Add(inventory);
        await _db.SaveChangesAsync();
        return inventory.Id;
    }

    private async Task<Guid> QueueRunAsync(Guid branchId)
    {
        var run = new BackgroundJobRun("Forecast", $"branch:{branchId}", DateTime.UtcNow, branchId);
        _db.BackgroundJobRuns.Add(run);
        await _db.SaveChangesAsync();
        return run.Id;
    }

    private async Task<Guid> SeedSucceededRunTodayAsync(Guid branchId, Guid inventoryId)
    {
        var runId = await QueueRunAsync(branchId);
        var run = await _db.BackgroundJobRuns.SingleAsync(candidate => candidate.Id == runId);
        var token = Guid.NewGuid().ToString();
        run.Start(token, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
        run.MarkAsSucceeded(token, DateTime.UtcNow);
        await _db.SaveChangesAsync();

        _db.DemandForecasts.Add(DemandForecast.Create(
            inventoryId, 7, DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow), 0m, 0,
            ForecastDataQuality.Insufficient, "sma-v1", DateTime.UtcNow, runId));
        await _db.SaveChangesAsync();

        return runId;
    }

    [Fact]
    public async Task GetDueJobsAsync_BeforeForecastHour_ReturnsNothing()
    {
        var branchId = await SeedBranchAsync("Early Branch");
        await SeedInventoryAsync(branchId);

        var due = await _schedule.GetDueJobsAsync(AtUtcHour(0), CancellationToken.None);

        Assert.Empty(due);
    }

    [Fact]
    public async Task GetDueJobsAsync_AtOrAfterHour_QueuesOnePerActiveBranch()
    {
        var branchId = await SeedBranchAsync("Due Branch");
        await SeedInventoryAsync(branchId);
        await SeedBranchAsync("Inactive Branch", active: false);

        var due = await _schedule.GetDueJobsAsync(AtUtcHour(2), CancellationToken.None);

        var request = Assert.Single(due);
        Assert.Equal("Forecast", request.JobName);
        Assert.Equal($"branch:{branchId}", request.LockKey);
        Assert.Equal(branchId, request.BranchId);
    }

    [Fact]
    public async Task GetDueJobsAsync_WithActiveLock_SkipsThatBranch()
    {
        var lockedBranchId = await SeedBranchAsync("Locked Branch");
        await SeedInventoryAsync(lockedBranchId);
        var freeBranchId = await SeedBranchAsync("Free Branch");
        await SeedInventoryAsync(freeBranchId);
        await QueueRunAsync(lockedBranchId);

        var due = await _schedule.GetDueJobsAsync(AtUtcHour(2), CancellationToken.None);

        var request = Assert.Single(due);
        Assert.Equal($"branch:{freeBranchId}", request.LockKey);
    }

    [Fact]
    public async Task GetDueJobsAsync_WithSucceededRunToday_SkipsThatBranch()
    {
        var forecastedBranchId = await SeedBranchAsync("Forecasted Branch");
        var inventoryId = await SeedInventoryAsync(forecastedBranchId);
        await SeedSucceededRunTodayAsync(forecastedBranchId, inventoryId);
        var freshBranchId = await SeedBranchAsync("Fresh Branch");
        await SeedInventoryAsync(freshBranchId);

        var due = await _schedule.GetDueJobsAsync(AtUtcHour(2), CancellationToken.None);

        var request = Assert.Single(due);
        Assert.Equal($"branch:{freshBranchId}", request.LockKey);
    }

    [Fact]
    public async Task GetDueJobsAsync_SuccessfulEmptyBranch_IsNotRequeuedWithinSameDay()
    {
        var emptyBranchId = await SeedBranchAsync("Empty Forecasted Branch");
        var runId = await QueueRunAsync(emptyBranchId);
        var run = await _db.BackgroundJobRuns.SingleAsync(candidate => candidate.Id == runId);
        var token = Guid.NewGuid().ToString();
        run.Start(token, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
        run.MarkAsSucceeded(token, DateTime.UtcNow);
        await _db.SaveChangesAsync();

        var queuedBranchId = await SeedBranchAsync("Queued Branch");

        var due = await _schedule.GetDueJobsAsync(AtUtcHour(2), CancellationToken.None);

        var request = Assert.Single(due);
        Assert.Equal($"branch:{queuedBranchId}", request.LockKey);
    }

    [Fact]
    public async Task GetDueJobsAsync_CrossMidnight_CompletedYesterdayIsStillDueToday()
    {
        var yesterdayBranchId = await SeedBranchAsync("Yesterday Branch");
        var inventoryId = await SeedInventoryAsync(yesterdayBranchId);
        var startOfTodayUtc = DateOnly.FromDateTime(AtUtcHour(0)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        await SeedSucceededRunCompletedAtAsync(yesterdayBranchId, inventoryId, startOfTodayUtc.AddMinutes(-1));

        var due = await _schedule.GetDueJobsAsync(AtUtcHour(2), CancellationToken.None);

        // Completed at 23:59 yesterday does not count as "forecasted today", so the branch is still due.
        Assert.Contains(due, r => r.BranchId == yesterdayBranchId);
    }

    [Fact]
    public async Task GetDueJobsAsync_CrossMidnight_CompletedJustAfterMidnightIsSkippedToday()
    {
        var todayBranchId = await SeedBranchAsync("Midnight Branch");
        var inventoryId = await SeedInventoryAsync(todayBranchId);
        var startOfTodayUtc = DateOnly.FromDateTime(AtUtcHour(0)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        await SeedSucceededRunCompletedAtAsync(todayBranchId, inventoryId, startOfTodayUtc.AddSeconds(30));

        var due = await _schedule.GetDueJobsAsync(AtUtcHour(2), CancellationToken.None);

        Assert.DoesNotContain(due, r => r.BranchId == todayBranchId);
    }

    private async Task SeedSucceededRunCompletedAtAsync(Guid branchId, Guid inventoryId, DateTime completedAtUtc)
    {
        var runId = await QueueRunAsync(branchId);
        var run = await _db.BackgroundJobRuns.SingleAsync(candidate => candidate.Id == runId);
        var token = Guid.NewGuid().ToString();
        run.Start(token, completedAtUtc.AddMinutes(-5), completedAtUtc.AddMinutes(5));
        run.MarkAsSucceeded(token, completedAtUtc);
        await _db.SaveChangesAsync();

        _db.DemandForecasts.Add(DemandForecast.Create(
            inventoryId, 7, DateOnly.FromDateTime(completedAtUtc),
            DateOnly.FromDateTime(completedAtUtc), 0m, 0,
            ForecastDataQuality.Insufficient, "sma-v1", completedAtUtc, runId));
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task ScheduledForecastRun_CarriesBranchId_AndHandlerProcessesIt()
    {
        var branchId = await SeedBranchAsync("Scheduled Run Branch");
        var inventoryId = await SeedInventoryAsync(branchId);
        var runId = await QueueRunAsync(branchId);
        var queued = await _db.BackgroundJobRuns.SingleAsync(candidate => candidate.Id == runId);
        queued.Start("handler-token", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(10));
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(runId, CancellationToken.None);

        var forecasts = await _db.DemandForecasts
            .Where(x => x.JobRunId == runId && x.BranchInventoryId == inventoryId)
            .ToListAsync();
        Assert.Equal(2, forecasts.Count);
    }
}