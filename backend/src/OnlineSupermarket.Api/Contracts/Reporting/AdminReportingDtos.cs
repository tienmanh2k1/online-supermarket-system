namespace OnlineSupermarket.Api.Contracts.Reporting;

public sealed record DashboardSummaryDto(
    int TotalOrders,
    int PendingOrders,
    decimal CompletedRevenue,
    int LowStockItems,
    IReadOnlyList<RecentOrderDto> RecentOrders);

public sealed record RecentOrderDto(
    Guid Id,
    DateTime CreatedAtUtc,
    decimal TotalAmount,
    string Status,
    string FulfillmentType,
    int ItemCount);

public sealed record SalesReportDto(
    DateOnly From,
    DateOnly To,
    decimal TotalRevenue,
    int CompletedOrderCount,
    decimal AverageOrderValue,
    IReadOnlyList<DailySalesDto> Daily);

public sealed record DailySalesDto(DateOnly Date, decimal Revenue, int OrderCount);
