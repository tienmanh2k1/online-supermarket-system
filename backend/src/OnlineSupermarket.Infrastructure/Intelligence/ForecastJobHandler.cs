using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Intelligence;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Intelligence;

public class ForecastJobHandler(AppDbContext dbContext, TimeProvider timeProvider) : IBackgroundJobHandler
{
    private const int MaxObservationDays = 28;
    private const string AlgorithmVersion = "sma-v1";

    public string JobName => "Forecast";

    public async Task HandleAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await dbContext.BackgroundJobRuns.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == runId, cancellationToken);

        if (run == null)
        {
            throw new InvalidOperationException($"Forecast job run {runId} not found.");
        }

        if (run.BranchId == null || run.BranchId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Forecast run has no branch id.");
        }

        var branchId = run.BranchId.Value;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var observationEnd = DateOnly.FromDateTime(nowUtc.AddDays(-1));
        var windowStartUtc = observationEnd.AddDays(-(MaxObservationDays - 1))
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var inventories = await dbContext.BranchInventories.AsNoTracking()
            .Where(inventory => inventory.BranchId == branchId)
            .OrderBy(inventory => inventory.Id)
            .ToListAsync(cancellationToken);

        var inventoryIds = inventories.Select(inventory => inventory.Id).ToArray();
        if (inventoryIds.Length == 0)
        {
            return;
        }

        // Join to branch_inventories instead of filtering by Contains(inventoryIds):
        // MySql.EntityFrameworkCore has a parameter-binding NRE on Contains over a bound list,
        // so scope sales by the run's branch through the inventory join.
        var sales = await (from sale in dbContext.InventoryTransactions.AsNoTracking()
                join inventory in dbContext.BranchInventories.AsNoTracking()
                    on sale.BranchInventoryId equals inventory.Id
                where inventory.BranchId == branchId
                    && sale.TransactionType == InventoryTransactionType.Sale
                    && sale.CreatedAtUtc >= windowStartUtc
                select new { sale.BranchInventoryId, sale.CreatedAtUtc, sale.QuantityOnHandDelta })
            .ToListAsync(cancellationToken);

        var dailySalesByInventory = new Dictionary<Guid, Dictionary<DateOnly, int>>();
        foreach (var sale in sales)
        {
            if (sale.QuantityOnHandDelta >= 0)
            {
                continue;
            }

            var daily = dailySalesByInventory.TryGetValue(sale.BranchInventoryId, out var stored)
                ? stored
                : new Dictionary<DateOnly, int>();
            dailySalesByInventory[sale.BranchInventoryId] = daily;
            var day = DateOnly.FromDateTime(sale.CreatedAtUtc);
            var count = daily.TryGetValue(day, out var existing) ? existing : 0;
            daily[day] = count + Math.Abs(sale.QuantityOnHandDelta);
        }

        var rows = new List<DemandForecast>();
        foreach (var inventory in inventories)
        {
            var dailySales = dailySalesByInventory.TryGetValue(inventory.Id, out var stored)
            ? stored
            : new Dictionary<DateOnly, int>();
            foreach (var horizon in new[] { DemandForecastCalculator.ShortHorizonDays, DemandForecastCalculator.LongHorizonDays })
            {
                var calculation = DemandForecastCalculator.Calculate(dailySales, observationEnd, horizon);
                rows.Add(DemandForecast.Create(
                    inventory.Id,
                    calculation.HorizonDays,
                    calculation.ForecastStartDate,
                    calculation.ForecastEndDate,
                    calculation.PredictedQuantity,
                    calculation.ActualDataDays,
                    calculation.DataQuality,
                    AlgorithmVersion,
                    nowUtc,
                    runId));
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await JobRunPublishGuard.EnsureOwnedAsync(dbContext, runId, timeProvider, cancellationToken);
            dbContext.DemandForecasts.AddRange(rows);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}