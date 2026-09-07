using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Api.Contracts.Inventory;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Intelligence;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Identity;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests.Endpoints;

public sealed class ForecastEndpointsTests
{
    private sealed record ForecastSeed(
        HttpClient AdminClient,
        HttpClient CustomerClient,
        Guid BranchId,
        Guid BranchInventoryId,
        Guid SuccessRunId);

    private static async Task<ForecastSeed> SeedAsync(TestApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var admin = User.Create(
            $"forecast_admin_{Guid.NewGuid():N}@test.com", hasher.HashPassword("Password123!"),
            "Admin", null, UserRole.Admin);
        var customer = User.Create(
            $"forecast_customer_{Guid.NewGuid():N}@test.com", hasher.HashPassword("Password123!"),
            "Customer", null, UserRole.Customer);
        db.Users.Add(admin);
        db.Users.Add(customer);
        await db.SaveChangesAsync();

        var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(admin));
        var customerClient = factory.CreateClient();
        customerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(customer));

        var branch = new Branch("Forecast Endpoint Branch", "1 Test Street", "0100000000", 10m, 106m);
        var category = new Category("ForecastEndpointCats", "forecast-endpoint-cats");
        var brand = new Brand("ForecastEndpointBrand", "forecast-endpoint-brand");
        db.Branches.Add(branch);
        db.Categories.Add(category);
        db.Brands.Add(brand);
        await db.SaveChangesAsync();

        var product = new Product(category.Id, brand.Id, "SKU-FE-1", "Forecast Endpoint Product", "forecast-endpoint-product",
            "desc", 40_000m, "cái", null);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var inventory = BranchInventory.Create(branch.Id, product.Id, 40_000m, 100, 5);
        db.BranchInventories.Add(inventory);
        await db.SaveChangesAsync();

        var run = new BackgroundJobRun("Forecast", $"branch:{branch.Id}", DateTime.UtcNow.AddHours(-2));
        var token = Guid.NewGuid().ToString();
        run.Start(token, DateTime.UtcNow.AddHours(-2).AddSeconds(1), DateTime.UtcNow.AddHours(-1));
        run.MarkAsSucceeded(token, DateTime.UtcNow.AddHours(-2).AddSeconds(5));
        db.BackgroundJobRuns.Add(run);
        await db.SaveChangesAsync();

        var generatedAt = DateTime.UtcNow.AddHours(-1);
        db.DemandForecasts.Add(DemandForecast.Create(
            inventory.Id, 7, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1),
            21.5m, 28, ForecastDataQuality.Sufficient, "sma-v1", generatedAt, run.Id));
        db.DemandForecasts.Add(DemandForecast.Create(
            inventory.Id, 14, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1),
            43m, 28, ForecastDataQuality.Sufficient, "sma-v1", generatedAt, run.Id));
        await db.SaveChangesAsync();

        return new ForecastSeed(adminClient, customerClient, branch.Id, inventory.Id, run.Id);
    }

    private static async Task<Guid> SeedFailedRunAsync(TestApiFactory factory, Guid branchId, Guid inventoryId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var run = new BackgroundJobRun("Forecast", $"branch:{branchId}", DateTime.UtcNow.AddHours(-5));
        var token = Guid.NewGuid().ToString();
        run.Start(token, DateTime.UtcNow.AddHours(-5).AddSeconds(1), DateTime.UtcNow.AddHours(-4));
        run.MarkAsFailed(token, DateTime.UtcNow.AddHours(-5).AddSeconds(2), "synthetic failure");
        db.BackgroundJobRuns.Add(run);
        await db.SaveChangesAsync();

        db.DemandForecasts.Add(DemandForecast.Create(
            inventoryId, 7, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1),
            99m, 28, ForecastDataQuality.Sufficient, "sma-v1", DateTime.UtcNow.AddHours(-4), run.Id));
        await db.SaveChangesAsync();

        return run.Id;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(30)]
    public async Task GetForecast_WithUnsupportedHorizon_ReturnsBadRequest(int horizon)
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);

        var response = await seed.AdminClient.GetAsync(
            $"/api/admin/forecast?branchId={seed.BranchId}&horizonDays={horizon}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetForecast_WithoutAuthentication_Returns401()
    {
        using var factory = new TestApiFactory();

        var response = await factory.CreateClient().GetAsync(
            "/api/admin/forecast?branchId=00000000-0000-0000-0000-000000000001&horizonDays=7");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetForecast_AsCustomer_Returns403()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);

        var response = await seed.CustomerClient.GetAsync(
            $"/api/admin/forecast?branchId={seed.BranchId}&horizonDays=7");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetForecast_WithUnknownBranch_Returns404()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);
        var unknownBranchId = Guid.NewGuid();

        var response = await seed.AdminClient.GetAsync(
            $"/api/admin/forecast?branchId={unknownBranchId}&horizonDays=7");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetForecast_AsAdmin_ReturnsLatestSuccessfulRows()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);

        var response = await seed.AdminClient.GetAsync(
            $"/api/admin/forecast?branchId={seed.BranchId}&horizonDays=7");
        var body = await response.Content.ReadFromJsonAsync<List<ForecastDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = Assert.Single(body!);
        Assert.Equal(seed.SuccessRunId, row.JobRunId);
        Assert.Equal(7, row.HorizonDays);
        Assert.Equal(21.5m, row.PredictedQuantity);
        Assert.Equal("Forecast Endpoint Product", row.ProductName);
        Assert.Equal("Sufficient", row.DataQuality);
        Assert.Equal(seed.BranchInventoryId, row.BranchInventoryId);
    }

    [Fact]
    public async Task GetForecast_IgnoresRowsFromFailedRuns()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);
        var failedRunId = await SeedFailedRunAsync(factory, seed.BranchId, seed.BranchInventoryId);

        var response = await seed.AdminClient.GetAsync(
            $"/api/admin/forecast?branchId={seed.BranchId}&horizonDays=7");
        var body = await response.Content.ReadFromJsonAsync<List<ForecastDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = Assert.Single(body!);
        Assert.Equal(seed.SuccessRunId, row.JobRunId);
        Assert.NotEqual(failedRunId, row.JobRunId);
        Assert.NotEqual(99m, row.PredictedQuantity);
    }

    [Fact]
    public async Task TriggerForecast_WithoutAuthentication_Returns401()
    {
        using var factory = new TestApiFactory();

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/admin/jobs/forecast/runs", new { branchId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TriggerForecast_AsCustomer_Returns403()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);

        var response = await seed.CustomerClient.PostAsJsonAsync(
            "/api/admin/jobs/forecast/runs", new { branchId = seed.BranchId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TriggerForecast_WithUnknownBranch_Returns404()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);

        var response = await seed.AdminClient.PostAsJsonAsync(
            "/api/admin/jobs/forecast/runs", new { branchId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TriggerForecast_WithEmptyBranchId_Returns400()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);

        var response = await seed.AdminClient.PostAsJsonAsync(
            "/api/admin/jobs/forecast/runs", new { branchId = Guid.Empty });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TriggerForecast_WhenAccepted_Returns202WithStatusUrl()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);

        var response = await seed.AdminClient.PostAsJsonAsync(
            "/api/admin/jobs/forecast/runs", new { branchId = seed.BranchId });
        var body = await response.Content.ReadFromJsonAsync<TriggerForecastRunResponse>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(body!.JobRunId != Guid.Empty);
        Assert.StartsWith("/api/admin/jobs/", body.StatusUrl);
    }

    [Fact]
    public async Task TriggerForecast_WithActiveLock_Returns409()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.BackgroundJobRuns.Add(new BackgroundJobRun(
                "Forecast", $"branch:{seed.BranchId}", DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        var response = await seed.AdminClient.PostAsJsonAsync(
            "/api/admin/jobs/forecast/runs", new { branchId = seed.BranchId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}