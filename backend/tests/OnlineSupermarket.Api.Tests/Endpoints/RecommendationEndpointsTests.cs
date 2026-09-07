using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Api.Contracts.Recommendation;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Infrastructure.Identity;
using OnlineSupermarket.Infrastructure.Persistence;
using OnlineSupermarket.Infrastructure.Recommendations;

namespace OnlineSupermarket.Api.Tests.Endpoints;

public sealed class ProductViewEventEndpointsTests
{
    private sealed record CaptureSeed(
        HttpClient Client,
        Guid UserId,
        Guid BranchId,
        Guid ProductId);

    private static async Task<CaptureSeed> SeedCatalogAsync(
        TestApiFactory factory,
        bool authenticate = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var branch = new Branch("View Branch", "1 Test Street", "0900000000", 10m, 106m);
        var category = new Category("ViewCats", "view-cats");
        var brand = new Brand("ViewBrand", "view-brand");
        db.Branches.Add(branch);
        db.Categories.Add(category);
        db.Brands.Add(brand);
        await db.SaveChangesAsync();

        var product = new Product(
            category.Id, brand.Id, $"SKU-{Guid.NewGuid():N}", "View Product", $"view-slug-{Guid.NewGuid():N}",
            "desc", 50_000m, "cái", null);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var client = factory.CreateClient();

        if (authenticate)
        {
            var user = User.Create($"view_{Guid.NewGuid():N}@test.com", "hash", "Viewer", null);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(user));
        }

        return new CaptureSeed(client, userId, branch.Id, product.Id);
    }

    [Fact]
    public async Task RecordView_AsGuest_PersistsAnonymousOwnerWithApprovedFields()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedCatalogAsync(factory);
        var sessionId = Guid.NewGuid();

        var response = await seed.Client.PostAsJsonAsync(
            $"/api/products/{seed.ProductId}/view-events",
            new { anonymousSessionId = sessionId, branchId = seed.BranchId });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.ProductViewEvents.SingleAsync();
        Assert.Null(stored.UserId);
        Assert.Equal(sessionId, stored.AnonymousSessionId);
        Assert.Equal(seed.ProductId, stored.ProductId);
        Assert.Equal(seed.BranchId, stored.BranchId);
    }

    [Fact]
    public async Task RecordView_WithUnknownBranch_Returns404BranchNotFound_AndDoesNotInsert()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedCatalogAsync(factory);
        var sessionId = Guid.NewGuid();

        var response = await seed.Client.PostAsJsonAsync(
            $"/api/products/{seed.ProductId}/view-events",
            new { anonymousSessionId = sessionId, branchId = Guid.NewGuid() });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("BRANCH_NOT_FOUND", body);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.ProductViewEvents.CountAsync());
    }

    [Fact]
    public async Task RecordView_WithOmittedBranch_IsAccepted_WithNullBranch()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedCatalogAsync(factory);
        var sessionId = Guid.NewGuid();

        var response = await seed.Client.PostAsJsonAsync(
            $"/api/products/{seed.ProductId}/view-events",
            new { anonymousSessionId = sessionId });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.ProductViewEvents.SingleAsync();
        Assert.Null(stored.BranchId);
    }

    [Fact]
    public async Task RecordView_AsAuthenticatedUser_PrefersJwtOwnerAndIgnoresBody()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedCatalogAsync(factory, authenticate: true);
        var sessionId = Guid.NewGuid();

        var response = await seed.Client.PostAsJsonAsync(
            $"/api/products/{seed.ProductId}/view-events",
            new { anonymousSessionId = sessionId });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.ProductViewEvents.SingleAsync();
        Assert.Equal(seed.UserId, stored.UserId);
        Assert.Null(stored.AnonymousSessionId);
    }

    [Fact]
    public async Task RecordView_WithEmptyAnonymousSessionId_ReturnsBadRequest()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedCatalogAsync(factory);

        var response = await seed.Client.PostAsJsonAsync(
            $"/api/products/{seed.ProductId}/view-events",
            new { anonymousSessionId = Guid.Empty });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecordView_MissingProduct_ReturnsNotFound()
    {
        using var factory = new TestApiFactory();
        var seed = await SeedCatalogAsync(factory);

        var response = await seed.Client.PostAsJsonAsync(
            $"/api/products/{Guid.NewGuid()}/view-events",
            new { anonymousSessionId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed class SessionMerge
{
    private sealed class ProductViewEventStoreStub : IProductViewEventStore
    {
        private readonly Queue<int> _results = new();

        public Guid? LastAnonymousSessionId { get; private set; }
        public Guid? LastUserId { get; private set; }

        public void Enqueue(int mergedCount) => _results.Enqueue(mergedCount);

        public Task<int> MergeAnonymousSessionAsync(
            Guid anonymousSessionId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            LastAnonymousSessionId = anonymousSessionId;
            LastUserId = userId;
            return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : 0);
        }
    }

    private sealed class SessionMergeTestFactory : TestApiFactory
    {
        public ProductViewEventStoreStub Store { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                var descriptor = services.Single(
                    d => d.ServiceType == typeof(IProductViewEventStore));
                services.Remove(descriptor);
                services.AddScoped<IProductViewEventStore>(_ => Store);
            });
        }
    }

    private static async Task<(HttpClient Client, Guid UserId)> CreateCustomerClientAsync(
        SessionMergeTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var customer = User.Create($"merge_{Guid.NewGuid():N}@test.com", "hash", "Merger", null);
        db.Users.Add(customer);
        await db.SaveChangesAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(customer));
        return (client, customer.Id);
    }

    [Fact]
    public async Task Merge_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new SessionMergeTestFactory();
        var anonymous = factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync(
            "/api/recommendations/session/merge",
            new { anonymousSessionId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Merge_WithEmptyAnonymousSessionId_ReturnsBadRequest()
    {
        using var factory = new SessionMergeTestFactory();
        var (client, _) = await CreateCustomerClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/session/merge",
            new { anonymousSessionId = Guid.Empty });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Merge_ClaimsSessionForJwtOwner_ReturnsMergedCount()
    {
        using var factory = new SessionMergeTestFactory();
        factory.Store.Enqueue(2);
        var (client, userId) = await CreateCustomerClientAsync(factory);
        var sessionId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/session/merge",
            new { anonymousSessionId = sessionId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MergeSessionResponse>();
        Assert.Equal(2, body!.MergedCount);
        Assert.Equal(sessionId, factory.Store.LastAnonymousSessionId);
        Assert.Equal(userId, factory.Store.LastUserId);
    }

    [Fact]
    public async Task Merge_RepeatedCall_ReturnsZeroOnSecondMerge()
    {
        using var factory = new SessionMergeTestFactory();
        factory.Store.Enqueue(2);
        factory.Store.Enqueue(0);
        var (client, _) = await CreateCustomerClientAsync(factory);
        var sessionId = Guid.NewGuid();

        var first = await client.PostAsJsonAsync(
            "/api/recommendations/session/merge",
            new { anonymousSessionId = sessionId });
        var second = await client.PostAsJsonAsync(
            "/api/recommendations/session/merge",
            new { anonymousSessionId = sessionId });

        Assert.Equal(2, (await first.Content.ReadFromJsonAsync<MergeSessionResponse>())!.MergedCount);
        Assert.Equal(0, (await second.Content.ReadFromJsonAsync<MergeSessionResponse>())!.MergedCount);
    }

    [Fact]
    public async Task Merge_IgnoresUserIdInBody()
    {
        using var factory = new SessionMergeTestFactory();
        factory.Store.Enqueue(1);
        var (client, userId) = await CreateCustomerClientAsync(factory);
        var bodyUserId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/session/merge",
            new { anonymousSessionId = Guid.NewGuid(), userId = bodyUserId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(userId, factory.Store.LastUserId);
        Assert.NotEqual(bodyUserId, factory.Store.LastUserId);
    }
}