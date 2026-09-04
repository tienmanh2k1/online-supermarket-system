using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Api.Contracts.Inventory;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Endpoints;

public static class InventoryIntelligenceEndpoints
{
    private const string ForecastJobName = "Forecast";
    private const string BranchLockPrefix = "branch:";

    public static IEndpointRouteBuilder MapInventoryIntelligenceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/admin/inventory")
            .WithTags("Admin-Inventory-Intelligence")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/{inventoryId:guid}/transactions", GetTransactionsAsync)
            .WithName("GetInventoryTransactions")
            .Produces<PaginatedInventoryTransactionsDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var adminGroup = routes.MapGroup("/api/admin")
            .WithTags("Admin-Forecast")
            .RequireAuthorization("AdminOnly");

        adminGroup.MapGet("/forecast", GetForecastAsync)
            .WithName("GetForecast")
            .Produces<IReadOnlyList<ForecastDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        adminGroup.MapPost("/jobs/forecast/runs", TriggerForecastRunAsync)
            .WithName("TriggerForecastRun")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return routes;
    }

    private static async Task<IResult> GetTransactionsAsync(
        [FromRoute] Guid inventoryId,
        [FromServices] AppDbContext dbContext,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var exists = await dbContext.BranchInventories
            .AnyAsync(bi => bi.Id == inventoryId, cancellationToken);

        if (!exists)
            return Results.NotFound(new { message = "Inventory not found." });

        var query = dbContext.InventoryTransactions
            .Where(t => t.BranchInventoryId == inventoryId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new InventoryTransactionDto(
                t.Id,
                t.BranchInventoryId,
                t.TransactionType.ToString(),
                t.QuantityOnHandDelta,
                t.ReservedQuantityDelta,
                t.QuantityOnHandAfter,
                t.ReservedQuantityAfter,
                t.ReferenceType.ToString(),
                t.ReferenceId,
                t.ActorUserId,
                t.Note,
                t.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PaginatedInventoryTransactionsDto(items, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetForecastAsync(
        [FromQuery] Guid branchId,
        [FromQuery] int horizonDays,
        [FromServices] AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (horizonDays is not (7 or 14))
        {
            return Results.BadRequest(new { message = "Horizon must be exactly 7 or 14 days." });
        }

        if (branchId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Branch id is required." });
        }

        var branchExists = await dbContext.Branches
            .AnyAsync(branch => branch.Id == branchId, cancellationToken);
        if (!branchExists)
        {
            return Results.NotFound(new { message = "Branch not found." });
        }

        var inventoryIds = await dbContext.BranchInventories.AsNoTracking()
            .Where(inventory => inventory.BranchId == branchId)
            .Select(inventory => inventory.Id)
            .ToListAsync(cancellationToken);

        if (inventoryIds.Count == 0)
        {
            return Results.Ok(Array.Empty<ForecastDto>());
        }

        var latestRunId = await dbContext.BackgroundJobRuns.AsNoTracking()
            .Where(run => run.JobName == ForecastJobName && run.Status == JobRunStatus.Succeeded)
            .Where(run => dbContext.DemandForecasts.Any(forecast =>
                forecast.JobRunId == run.Id && inventoryIds.Contains(forecast.BranchInventoryId)))
            .OrderByDescending(run => run.CompletedAtUtc ?? run.CreatedAtUtc)
            .Select(run => run.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestRunId == Guid.Empty)
        {
            return Results.Ok(Array.Empty<ForecastDto>());
        }

        var rows = await (from forecast in dbContext.DemandForecasts.AsNoTracking()
            join inventory in dbContext.BranchInventories
                on forecast.BranchInventoryId equals inventory.Id
            join product in dbContext.Products
                on inventory.ProductId equals product.Id
            where forecast.JobRunId == latestRunId
                && forecast.HorizonDays == horizonDays
                && inventoryIds.Contains(forecast.BranchInventoryId)
            orderby forecast.HorizonDays, product.Name
            select new
            {
                forecast.Id,
                forecast.BranchInventoryId,
                ProductId = product.Id,
                product.Name,
                forecast.HorizonDays,
                forecast.PredictedQuantity,
                forecast.ActualDataDays,
                forecast.DataQuality,
                forecast.ForecastStartDate,
                forecast.ForecastEndDate,
                forecast.GeneratedAtUtc,
                forecast.JobRunId,
            }).ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new ForecastDto(
                row.Id, row.BranchInventoryId, row.ProductId, row.Name,
                row.HorizonDays, row.PredictedQuantity, row.ActualDataDays,
                row.DataQuality.ToString(), row.ForecastStartDate, row.ForecastEndDate,
                row.GeneratedAtUtc, row.JobRunId))
            .ToList();

        return Results.Ok(items);
    }

    private static async Task<IResult> TriggerForecastRunAsync(
        [FromBody] TriggerForecastRunRequest request,
        [FromServices] AppDbContext dbContext,
        [FromServices] JobRunCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        if (request.BranchId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Branch id is required." });
        }

        var branchExists = await dbContext.Branches
            .AnyAsync(branch => branch.Id == request.BranchId, cancellationToken);
        if (!branchExists)
        {
            return Results.NotFound(new { message = "Branch not found." });
        }

        var lockKey = BranchLockPrefix + request.BranchId;
        var run = await coordinator.TryQueueAsync(
            ForecastJobName, lockKey, cancellationToken, request.BranchId);
        if (run == null)
        {
            return Results.Conflict(new { message = "A forecast run for this branch is already active." });
        }

        var statusUrl = $"/api/admin/jobs/{run.Value}";
        return Results.Accepted(
            statusUrl,
            new TriggerForecastRunResponse(run.Value, statusUrl));
    }
}