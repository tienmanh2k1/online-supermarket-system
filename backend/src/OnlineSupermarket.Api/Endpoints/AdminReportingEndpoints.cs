using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Api.Contracts.Reporting;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Endpoints;

public static class AdminReportingEndpoints
{
    public static IEndpointRouteBuilder MapAdminReportingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/admin")
            .WithTags("Admin-Reporting")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/dashboard/summary", GetDashboardSummaryAsync)
            .WithName("GetDashboardSummary")
            .Produces<DashboardSummaryDto>(StatusCodes.Status200OK);

        group.MapGet("/reports/sales", GetSalesReportAsync)
            .WithName("GetSalesReport")
            .Produces<SalesReportDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return routes;
    }

    private static async Task<IResult> GetDashboardSummaryAsync(
        [FromServices] AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actionableStatuses = new[]
        {
            OrderStatus.Pending,
            OrderStatus.Confirmed,
            OrderStatus.Preparing,
            OrderStatus.Ready
        };

        var totalOrders = await dbContext.Orders.CountAsync(cancellationToken);
        var pendingOrders = await dbContext.Orders
            .CountAsync(o => actionableStatuses.Contains(o.Status), cancellationToken);
        var completedRevenue = await dbContext.Orders
            .Where(o => o.Status == OrderStatus.Completed)
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;
        var lowStockItems = await dbContext.BranchInventories
            .CountAsync(i => i.ReorderLevel > 0 && i.QuantityOnHand <= i.ReorderLevel, cancellationToken);

        var recentOrders = await dbContext.Orders
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(5)
            .Select(o => new RecentOrderDto(
                o.Id,
                o.CreatedAtUtc,
                o.TotalAmount,
                o.Status.ToString(),
                o.FulfillmentType,
                o.Items.Count))
            .ToListAsync(cancellationToken);

        var summary = new DashboardSummaryDto(
            TotalOrders: totalOrders,
            PendingOrders: pendingOrders,
            CompletedRevenue: completedRevenue,
            LowStockItems: lowStockItems,
            RecentOrders: recentOrders);

        return Results.Ok(summary);
    }

    private static async Task<IResult> GetSalesReportAsync(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromServices] AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Validate date parameters
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid date range",
                detail: "Both 'from' and 'to' query parameters are required in yyyy-MM-dd format.");
        }

        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var fromDate) ||
            !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var toDate))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid date format",
                detail: "Use UTC dates in yyyy-MM-dd format.");
        }

        if (fromDate > toDate)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid date range",
                detail: "'from' must be less than or equal to 'to'.");
        }

        if (toDate == DateOnly.MaxValue)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid date range",
                detail: "End date cannot be the maximum date value.");
        }

        if (toDate.DayNumber - fromDate.DayNumber + 1 > 366)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid date range",
                detail: "Use UTC dates in yyyy-MM-dd format with at most 366 inclusive days.");
        }

        // Query completed orders within the date range
        var fromUtc = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtcExclusive = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var completedOrders = await dbContext.Orders
            .Where(o => o.Status == OrderStatus.Completed
                        && o.CreatedAtUtc >= fromUtc
                        && o.CreatedAtUtc < toUtcExclusive)
            .Select(o => new { o.CreatedAtUtc.Date, o.TotalAmount })
            .ToListAsync(cancellationToken);

        var totalRevenue = completedOrders.Sum(o => o.TotalAmount);
        var completedOrderCount = completedOrders.Count;
        var averageOrderValue = completedOrderCount > 0
            ? totalRevenue / completedOrderCount
            : 0m;

        // Generate daily breakdown with zero-fill
        var dailyData = completedOrders
            .GroupBy(o => DateOnly.FromDateTime(o.Date))
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(o => o.TotalAmount), OrderCount: g.Count()));

        var dailyList = new List<DailySalesDto>();
        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            if (dailyData.TryGetValue(date, out var data))
            {
                dailyList.Add(new DailySalesDto(date, data.Revenue, data.OrderCount));
            }
            else
            {
                dailyList.Add(new DailySalesDto(date, 0m, 0));
            }
        }

        var report = new SalesReportDto(
            From: fromDate,
            To: toDate,
            TotalRevenue: totalRevenue,
            CompletedOrderCount: completedOrderCount,
            AverageOrderValue: averageOrderValue,
            Daily: dailyList);

        return Results.Ok(report);
    }
}
