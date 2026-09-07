using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Api.Contracts.Recommendation;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Domain.Recommendations;
using OnlineSupermarket.Infrastructure.Identity;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests.Endpoints;

public sealed class RecommendationReadEndpointsTests
{
    private sealed record ReadSeed(
        HttpClient Client,
        Guid BranchId,
        Guid ProductAId,
        Guid ProductBId,
        Guid? UserId,
        Guid JobRunId);

    private static async Task<ReadSeed> SeedMaterializationAsync(
        TestApiFactory factory,
        bool withUser = false,
        bool withInventory = false,
        bool withExpiredUserRows = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var branch = new Branch("Read Branch", "1 Test Street", "0100000000", 10m, 106m);
        var category = new Category("ReadCats", "read-cats");
        var brand = new Brand("ReadBrand", "read-brand");
        var user = withUser
            ? User.Create($"read_{Guid.NewGuid():N}@test.com", "hash", "Reader", null)
            : null;
        db.Branches.Add(branch);
        db.Categories.Add(category);
        db.Brands.Add(brand);
        if (user != null)
        {
            db.Users.Add(user);
        }
        await db.SaveChangesAsync();

        var productA = new Product(category.Id, brand.Id, "SKU-R-1", "Read Product A", "read-product-a",
            "desc", 45_000m, "cái", null);
        var productB = new Product(category.Id, brand.Id, "SKU-R-2", "Read Product B", "read-product-b",
            "desc", 55_000m, "cái", null);
        var inactive = new Product(category.Id, brand.Id, "SKU-R-3", "Inactive Read", "inactive-read",
            "desc", 65_000m, "cái", null);
        inactive.Deactivate();
        db.Products.Add(productA);
        db.Products.Add(productB);
        db.Products.Add(inactive);
        await db.SaveChangesAsync();

        if (withInventory)
        {
            db.BranchInventories.AddRange(
                BranchInventory.Create(branch.Id, productA.Id, 45_000m, 5, 1),
                BranchInventory.Create(branch.Id, productB.Id, 55_000m, 0, 1));
            await db.SaveChangesAsync();
        }

        var run = new BackgroundJobRun("Recommendations", "global", DateTime.UtcNow.AddHours(-2));
        var lockToken = Guid.NewGuid().ToString();
        run.Start(lockToken, DateTime.UtcNow.AddHours(-2).AddSeconds(1), DateTime.UtcNow.AddHours(-1));
        run.MarkAsSucceeded(lockToken, DateTime.UtcNow.AddHours(-2).AddSeconds(5));
        db.BackgroundJobRuns.Add(run);
        await db.SaveChangesAsync();

        var generatedAt = DateTime.UtcNow.AddHours(-1);
        db.RecommendationResults.Add(RecommendationResult.CreateGlobal(
            productA.Id, 0.9m, 1, "Được nhiểu người quan tâm", "content-v1",
            generatedAt, generatedAt.AddHours(2), run.Id));
        db.RecommendationResults.Add(RecommendationResult.CreateGlobal(
            productB.Id, 0.8m, 2, "Được nhiểu người quan tâm", "content-v1",
            generatedAt, generatedAt.AddHours(2), run.Id));
        db.RecommendationResults.Add(RecommendationResult.CreateGlobal(
            inactive.Id, 0.7m, 3, "Được nhiểu người quan tâm", "content-v1",
            generatedAt, generatedAt.AddHours(2), run.Id));

        if (user != null)
        {
            var (userGeneratedAt, userExpiresAt) = withExpiredUserRows
                ? (DateTime.UtcNow.AddHours(-3), DateTime.UtcNow.AddHours(-1))
                : (generatedAt, generatedAt.AddHours(2));
            db.RecommendationResults.Add(RecommendationResult.CreateForUser(
                user.Id, productA.Id, 0.95m, 1, "Phù hợp danh mục đã xem", "content-v1",
                userGeneratedAt, userExpiresAt, run.Id));
            db.RecommendationResults.Add(RecommendationResult.CreateForUser(
                user.Id, productB.Id, 0.5m, 2, "Phù hợp danh mục đã xem", "content-v1",
                userGeneratedAt, userExpiresAt, run.Id));
        }

        db.RecommendationResults.Add(RecommendationResult.CreateSimilarProduct(
            productA.Id, productB.Id, 0.85m, 1, "Tương tự với sản phẩm đang xem", "content-v1",
            generatedAt, generatedAt.AddHours(2), run.Id));

        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        if (user != null)
        {
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(user));
        }

        return new ReadSeed(client, branch.Id, productA.Id, productB.Id, user?.Id, run.Id);
    }

    [Fact]
    public async Task Homepage_WhenUserRowsExpired_FallsBackToGlobalWith200()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedMaterializationAsync(factory, withUser: true, withInventory: true, withExpiredUserRows: true);

        var response = await seed.Client.GetAsync(
            "/api/recommendations?branchId=" + seed.BranchId);
        var body = await response.Content.ReadFromJsonAsync<RecommendationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Global", body!.SourceScope);
        Assert.All(body.Items, item => Assert.True(item.AvailableQuantity! > 0));
    }

    [Fact]
    public async Task Homepage_WithFreshUserRows_ReturnsUserScope()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedMaterializationAsync(factory, withUser: true);

        var response = await seed.Client.GetAsync("/api/recommendations");
        var body = await response.Content.ReadFromJsonAsync<RecommendationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("User", body!.SourceScope);
        Assert.True(body!.Items.Count > 0);
        Assert.Equal(seed.ProductAId, body.Items[0].ProductId);
    }

    [Fact]
    public async Task Homepage_WithBranchFilter_HidesUnavailableAndInactiveItems()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedMaterializationAsync(factory, withInventory: true);

        var response = await seed.Client.GetAsync(
            "/api/recommendations?branchId=" + seed.BranchId);
        var body = await response.Content.ReadFromJsonAsync<RecommendationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(seed.ProductAId, body!.Items.Single().ProductId);
        Assert.NotEqual("Inactive Read", body.Items.Single().Name);
    }

    [Fact]
    public async Task Homepage_WhenNoMaterialization_ReturnsEmpty200()
    {
        using var factory = new TestApiFactory();

        var response = await factory.CreateClient().GetAsync("/api/recommendations");
        var body = await response.Content.ReadFromJsonAsync<RecommendationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body!.Items.Count == 0);
        Assert.Null(body.SourceScope);
    }

    [Fact]
    public async Task ProductRecommendations_WhenNoMaterialization_ReturnsEmpty200()
    {
        using var factory = new TestApiFactory();
        var productId = Guid.NewGuid();

        var response = await factory.CreateClient().GetAsync(
            "/api/products/" + productId + "/recommendations");
        var body = await response.Content.ReadFromJsonAsync<RecommendationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body!.Items.Count == 0);
    }

    [Fact]
    public async Task ProductRecommendations_WithSimilarRows_ReturnsSimilarScope()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedMaterializationAsync(factory);

        var response = await seed.Client.GetAsync(
            "/api/products/" + seed.ProductAId + "/recommendations");
        var body = await response.Content.ReadFromJsonAsync<RecommendationResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("SimilarProduct", body!.SourceScope);
        Assert.Equal(seed.ProductBId, body!.Items.Single().ProductId);
    }

    [Fact]
    public async Task RecommendationQuery_WithOutOfRangeLimit_ReturnsBadRequest()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedMaterializationAsync(factory);

        var tooSmall = await seed.Client.GetAsync("/api/recommendations?limit=0");
        var tooLarge = await seed.Client.GetAsync("/api/recommendations?limit=21");

        Assert.Equal(HttpStatusCode.BadRequest, tooSmall.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
    }
}