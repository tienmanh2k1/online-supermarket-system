using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Api.Contracts.Reporting;
using OnlineSupermarket.Api.Tests.Auth;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Infrastructure.Identity;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests.Endpoints;

public sealed class AdminReportingEndpointsTests : IClassFixture<AuthTestApiFactory>
{
    private readonly AuthTestApiFactory _factory;

    public AdminReportingEndpointsTests(AuthTestApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(UserRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var email = $"{Guid.NewGuid()}@example.com";
        var user = User.Create(email, passwordHasher.HashPassword("Password123!"), "Test User", null, role);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var token = tokenService.GenerateAccessToken(user);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ==========================================
    // AUTHENTICATION & AUTHORIZATION
    // ==========================================

    [Theory]
    [InlineData("/api/admin/dashboard/summary")]
    [InlineData("/api/admin/reports/sales?from=2026-09-01&to=2026-09-09")]
    public async Task Reporting_WithoutToken_ReturnsUnauthorized(string path)
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/dashboard/summary")]
    [InlineData("/api/admin/reports/sales?from=2026-09-01&to=2026-09-09")]
    public async Task Reporting_WithCustomerToken_ReturnsForbidden(string path)
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Customer);
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DashboardSummary_AsAdmin_ReturnsOk()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.GetAsync("/api/admin/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SalesReport_AsAdmin_ReturnsOk()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.GetAsync("/api/admin/reports/sales?from=2026-09-01&to=2026-09-09");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ==========================================
    // DASHBOARD AGGREGATION
    // ==========================================

    [Fact]
    public async Task DashboardSummary_AggregatesCorrectly()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Clear existing data from seeder
        dbContext.Orders.RemoveRange(dbContext.Orders);
        dbContext.BranchInventories.RemoveRange(dbContext.BranchInventories);
        dbContext.Products.RemoveRange(dbContext.Products);
        dbContext.Categories.RemoveRange(dbContext.Categories);
        dbContext.Brands.RemoveRange(dbContext.Brands);
        await dbContext.SaveChangesAsync();

        // Create branch for inventory
        var branch = new Branch("Test Branch", "Address", null, 10.0m, 106.0m);
        dbContext.Branches.Add(branch);
        await dbContext.SaveChangesAsync();

        // Create category and product for inventory
        var category = new Category("Test Category", "test-cat-slug");
        dbContext.Categories.Add(category);
        var brand = new Brand("Test Brand", "test-brand-slug");
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var product = new Product(category.Id, brand.Id, "TEST-SKU", "Test Product", "test-product", null, 10m, "unit", null);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        // Create low stock inventory: Qty=2 <= ReorderLevel=5
        var lowStockInventory = BranchInventory.Create(branch.Id, product.Id, 10m, 2, 5);
        dbContext.BranchInventories.Add(lowStockInventory);

        // Create normal stock inventory: Qty=100 > ReorderLevel=10
        var normalInventory = BranchInventory.Create(branch.Id, product.Id, 10m, 100, 10);
        dbContext.BranchInventories.Add(normalInventory);
        await dbContext.SaveChangesAsync();

        // Create orders in various statuses
        var customer = User.Create("customer@test.com", "hash", "Customer", null, UserRole.Customer);
        dbContext.Users.Add(customer);
        await dbContext.SaveChangesAsync();

        var items = new List<(Guid, string, string, decimal, int, decimal)>
        {
            (product.Id, "Product", "sku", 10m, 1, 10m)
        };

        var pendingOrder = Order.Create(customer.Id, branch.Id, "Pickup", "Customer", "0123456789", "Pickup", null, items, 100m, 0m, 0m, 100m);
        pendingOrder.SetStatus(OrderStatus.Pending);
        dbContext.Entry(pendingOrder).Property("CreatedAtUtc").CurrentValue = DateTime.UtcNow.AddDays(-1);
        dbContext.Orders.Add(pendingOrder);

        var preparingOrder = Order.Create(customer.Id, branch.Id, "Pickup", "Customer", "0123456789", "Pickup", null, items, 200m, 0m, 0m, 200m);
        preparingOrder.SetStatus(OrderStatus.Preparing);
        dbContext.Entry(preparingOrder).Property("CreatedAtUtc").CurrentValue = DateTime.UtcNow.AddDays(-2);
        dbContext.Orders.Add(preparingOrder);

        var completedOrder = Order.Create(customer.Id, branch.Id, "Pickup", "Customer", "0123456789", "Pickup", null, items, 500m, 0m, 0m, 500m);
        completedOrder.SetStatus(OrderStatus.Completed);
        dbContext.Entry(completedOrder).Property("CreatedAtUtc").CurrentValue = DateTime.UtcNow.AddDays(-3);
        dbContext.Orders.Add(completedOrder);

        var cancelledOrder = Order.Create(customer.Id, branch.Id, "Pickup", "Customer", "0123456789", "Pickup", null, items, 300m, 0m, 0m, 300m);
        cancelledOrder.SetStatus(OrderStatus.Cancelled);
        dbContext.Entry(cancelledOrder).Property("CreatedAtUtc").CurrentValue = DateTime.UtcNow.AddDays(-4);
        dbContext.Orders.Add(cancelledOrder);

        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.GetAsync("/api/admin/dashboard/summary");
        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>();

        Assert.NotNull(summary);
        Assert.Equal(4, summary.TotalOrders);
        Assert.Equal(2, summary.PendingOrders); // Pending + Preparing
        Assert.Equal(500m, summary.CompletedRevenue); // Only Completed
        Assert.Equal(1, summary.LowStockItems); // Only one item <= reorder level
        Assert.NotNull(summary.RecentOrders);
    }

    [Fact]
    public async Task DashboardSummary_EmptyDatabase_ReturnsZeros()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Clear existing data from seeder
        dbContext.Orders.RemoveRange(dbContext.Orders);
        dbContext.BranchInventories.RemoveRange(dbContext.BranchInventories);
        dbContext.Products.RemoveRange(dbContext.Products);
        dbContext.Categories.RemoveRange(dbContext.Categories);
        dbContext.Branches.RemoveRange(dbContext.Branches);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.GetAsync("/api/admin/dashboard/summary");
        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>();

        Assert.NotNull(summary);
        Assert.Equal(0, summary.TotalOrders);
        Assert.Equal(0, summary.PendingOrders);
        Assert.Equal(0m, summary.CompletedRevenue);
        Assert.Equal(0, summary.LowStockItems);
        Assert.NotNull(summary.RecentOrders);
        Assert.Empty(summary.RecentOrders);
    }
}
