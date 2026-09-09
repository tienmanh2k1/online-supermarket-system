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
        var report = new SalesReportDto(
            From: DateOnly.FromDateTime(DateTime.UtcNow),
            To: DateOnly.FromDateTime(DateTime.UtcNow),
            TotalRevenue: 0m,
            CompletedOrderCount: 0,
            AverageOrderValue: 0m,
            Daily: Array.Empty<DailySalesDto>());

        return Results.Ok(report);
    }
}
