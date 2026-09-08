using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Api.Contracts.Recommendation;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Domain.Recommendations;
using OnlineSupermarket.Infrastructure.Identity;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests.Endpoints;

public sealed class AdminRecommendationEndpointsTests
{
    private static async Task<Guid> SeedFailedRunAsync(AppDbContext db)
    {
        var run = new BackgroundJobRun("Recommendations", "global", DateTime.UtcNow.AddHours(-4));
        var token = Guid.NewGuid().ToString();
        run.Start(token, DateTime.UtcNow.AddHours(-4).AddSeconds(1), DateTime.UtcNow.AddHours(-3));
        run.MarkAsFailed(token, DateTime.UtcNow.AddHours(-4).AddSeconds(2), "synthetic failure");
        db.BackgroundJobRuns.Add(run);
        await db.SaveChangesAsync();

        var generatedAt = DateTime.UtcNow.AddHours(-3);
        db.RecommendationResults.Add(RecommendationResult.CreateGlobal(
            Guid.NewGuid(), 0.5m, 1, "Stale", "content-v1",
            generatedAt, generatedAt.AddHours(2), run.Id));
        await db.SaveChangesAsync();

        return run.Id;
    }

    private sealed record AdminSeed(
        HttpClient AdminClient,
        HttpClient CustomerClient,
        Guid SuccessRunId,
        Guid ProductId);

    private static async Task<AdminSeed> SeedAsync(TestApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var admin = User.Create(
            $"admin_{Guid.NewGuid():N}@test.com", hasher.HashPassword("Password123!"),
            "Admin", null, UserRole.Admin);
        var customer = User.Create(
            $"customer_{Guid.NewGuid():N}@test.com", hasher.HashPassword("Password123!"),
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

        var branch = new Branch("Admin Rec Branch", "1 Test Street", "0100000000", 10m, 106m);
        var category = new Category("AdminRecCats", "admin-rec-cats");
        var brand = new Brand("AdminRecBrand", "admin-rec-brand");
        db.Branches.Add(branch);
        db.Categories.Add(category);
        db.Brands.Add(brand);
        await db.SaveChangesAsync();

        var product = new Product(category.Id, brand.Id, "SKU-AR-1", "Admin Rec Product", "admin-rec-product",
            "desc", 40_000m, "cái", null);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var run = new BackgroundJobRun("Recommendations", "global", DateTime.UtcNow.AddHours(-2));
        var token = Guid.NewGuid().ToString();
        run.Start(token, DateTime.UtcNow.AddHours(-2).AddSeconds(1), DateTime.UtcNow.AddHours(-1));
        run.MarkAsSucceeded(token, DateTime.UtcNow.AddHours(-2).AddSeconds(5));
        db.BackgroundJobRuns.Add(run);
        await db.SaveChangesAsync();

        var generatedAt = DateTime.UtcNow.AddHours(-1);
        var expiresAt = generatedAt.AddHours(2);
        db.RecommendationResults.Add(RecommendationResult.CreateGlobal(
            product.Id, 0.9m, 1, "Được nhiểu người quan tâm", "content-v1",
            generatedAt, expiresAt, run.Id));
        db.RecommendationResults.Add(RecommendationResult.CreateForUser(
            customer.Id, product.Id, 0.85m, 1, "Phù hợp danh mục đã xem", "content-v1",
            generatedAt, expiresAt, run.Id));
        await db.SaveChangesAsync();

        return new AdminSeed(adminClient, customerClient, run.Id, product.Id);
    }

    [Fact]
    public async Task AdminResults_WithoutAuthentication_Returns401()
    {
        using var factory = new TestApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/admin/recommendations/results");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminResults_AsCustomer_Returns403()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);

        var response = await seed.CustomerClient.GetAsync("/api/admin/recommendations/results");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminResults_AsAdmin_ReturnsScopeFilteredSample()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);

        var response = await seed.AdminClient.GetAsync(
            "/api/admin/recommendations/results?scope=Global&limit=5");
        var body = await response.Content.ReadFromJsonAsync<RecommendationSampleResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(seed.SuccessRunId, body!.JobRunId);
        Assert.Equal("content-v1", body.AlgorithmVersion);
        Assert.All(body.Items, item => Assert.Equal("Global", item.Scope));
        Assert.True(body.GeneratedAtUtc != DateTime.MinValue);
        Assert.True(body.ExpiresAtUtc > body.GeneratedAtUtc);
    }

    [Fact]
    public async Task AdminResults_WithInvalidScope_Returns400()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);

        var response = await seed.AdminClient.GetAsync(
            "/api/admin/recommendations/results?scope=Nonsense");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminResults_IgnoresFailedRunRows()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);
        var failedRunId = await SeedFailedRunAsync(
            factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>());

        var response = await seed.AdminClient.GetAsync("/api/admin/recommendations/results?limit=5");
        var body = await response.Content.ReadFromJsonAsync<RecommendationSampleResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(seed.SuccessRunId, body!.JobRunId);
        Assert.NotEqual(failedRunId, body.JobRunId);
    }

    [Fact]
    public async Task TriggerRun_WithoutAuthentication_Returns401()
    {
        using var factory = new TestApiFactory();

        var response = await factory.CreateClient().PostAsync(
            "/api/admin/jobs/recommendations/runs", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TriggerRun_AsAdmin_Returns202WithStatusUrl()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);

        var response = await seed.AdminClient.PostAsync(
            "/api/admin/jobs/recommendations/runs", null);
        var body = await response.Content.ReadFromJsonAsync<TriggerRecommendationRunResponse>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(body!.JobRunId != Guid.Empty);
        Assert.StartsWith("/api/admin/jobs/", body.StatusUrl);
    }

    [Fact]
    public async Task TriggerRun_WithActiveLock_Returns409()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.BackgroundJobRuns.Add(new BackgroundJobRun(
                "Recommendations", "global", DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        var response = await seed.AdminClient.PostAsync(
            "/api/admin/jobs/recommendations/runs", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task TriggerRun_AsCustomer_Returns403()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedAsync(factory);

        var response = await seed.CustomerClient.PostAsync(
            "/api/admin/jobs/recommendations/runs", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}