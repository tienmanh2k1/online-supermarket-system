using Microsoft.AspNetCore.Mvc;
using OnlineSupermarket.Api.Contracts.Reporting;
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
        var summary = new DashboardSummaryDto(
            TotalOrders: 0,
            PendingOrders: 0,
            CompletedRevenue: 0m,
            LowStockItems: 0,
            RecentOrders: Array.Empty<RecentOrderDto>());

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
