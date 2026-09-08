using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Recommendations;
using OnlineSupermarket.Infrastructure.Persistence;
using OnlineSupermarket.Infrastructure.Recommendations;
using OnlineSupermarket.Infrastructure.Tests.Persistence;
using Xunit;

namespace OnlineSupermarket.Infrastructure.Tests.Recommendations;

[Collection(MySqlInfrastructureCollection.Name)]
public sealed class ProductViewEventStoreTests : IClassFixture<MySqlFixture>, IAsyncLifetime
{
    private readonly MySqlFixture _fixture;
    private static Guid _productId;
    private static Guid _someOtherUserId;

    public ProductViewEventStoreTests(MySqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(_fixture.ConnectionString)
            .Options;

        await using var db = new AppDbContext(options);

        var slug = Guid.NewGuid().ToString("N");
        var category = new Category("View", slug);
        var brand = new Brand("View", slug);
        var branch = new Branch("View Branch", "1 Test Street", "0900000000", 10m, 106m);
        var product = new Product(category.Id, brand.Id, $"SKU-{slug[..8]}", "View Product", slug, "d", 10_000m, "cái", null);
        var otherUser = User.Create($"other_{Guid.NewGuid():N}@test.com", "hash", "Other", null);
        db.Categories.Add(category);
        db.Brands.Add(brand);
        db.Branches.Add(branch);
        db.Products.Add(product);
        db.Users.Add(otherUser);
        await db.SaveChangesAsync();

        _productId = product.Id;
        _someOtherUserId = otherUser.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static DbContextOptions<AppDbContext> Options(string connectionString) =>
        new DbContextOptionsBuilder<AppDbContext>().UseMySQL(connectionString).Options;

    private static async Task<Guid> SeedEventsAsync(string connectionString, Guid sessionId)
    {
        await using var db = new AppDbContext(Options(connectionString));

        db.ProductViewEvents.Add(ProductViewEvent.Create(_productId, null, sessionId, null, DateTime.UtcNow));
        db.ProductViewEvents.Add(ProductViewEvent.Create(_productId, null, sessionId, null, DateTime.UtcNow.AddSeconds(1)));
        db.ProductViewEvents.Add(ProductViewEvent.Create(_productId, _someOtherUserId, sessionId, null, DateTime.UtcNow.AddSeconds(2)));

        var userId = Guid.NewGuid();
        var user = User.Create($"merger_{Guid.NewGuid():N}@test.com", "hash", "Merger", null);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user.Id;
    }

    private static async Task<Guid> CreateUserAsync(string connectionString)
    {
        await using var db = new AppDbContext(Options(connectionString));
        var user = User.Create($"claimer_{Guid.NewGuid():N}@test.com", "hash", "Claimer", null);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Merge_ClaimsOnlyUnownedRowsAndReturnsDeterministicCount()
    {
        var connectionString = _fixture.ConnectionString;
        var sessionId = Guid.NewGuid();
        var mergerUserId = await SeedEventsAsync(connectionString, sessionId);

        await using var db = new AppDbContext(Options(connectionString));
        var store = new ProductViewEventStore(db);

        var merged = await store.MergeAnonymousSessionAsync(sessionId, mergerUserId, CancellationToken.None);

        Assert.Equal(2, merged);

        var rows = await db.ProductViewEvents.AsNoTracking().ToListAsync();
        var sessionRows = rows.Where(r => r.AnonymousSessionId == sessionId).ToList();
        Assert.Equal(2, sessionRows.Count(r => r.UserId == mergerUserId));
        Assert.Equal(1, sessionRows.Count(r => r.UserId == _someOtherUserId));
    }

    [Fact]
    public async Task Merge_RepeatedCall_ReturnsZero()
    {
        var connectionString = _fixture.ConnectionString;
        var sessionId = Guid.NewGuid();
        var mergerUserId = await SeedEventsAsync(connectionString, sessionId);

        await using var db = new AppDbContext(Options(connectionString));
        var store = new ProductViewEventStore(db);

        Assert.Equal(2, await store.MergeAnonymousSessionAsync(sessionId, mergerUserId, CancellationToken.None));
        Assert.Equal(0, await store.MergeAnonymousSessionAsync(sessionId, mergerUserId, CancellationToken.None));
    }

    [Fact]
    public async Task Merge_OwnedRows_AreNeverReassignedToAnotherUser()
    {
        var connectionString = _fixture.ConnectionString;
        var sessionId = Guid.NewGuid();

        await using (var seedDb = new AppDbContext(Options(connectionString)))
        {
            seedDb.ProductViewEvents.Add(
                ProductViewEvent.Create(_productId, _someOtherUserId, sessionId, null, DateTime.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        await using var db = new AppDbContext(Options(connectionString));
        var store = new ProductViewEventStore(db);
        var claimingUser = await CreateUserAsync(connectionString);

        Assert.Equal(0, await store.MergeAnonymousSessionAsync(sessionId, claimingUser, CancellationToken.None));

        var owned = await db.ProductViewEvents.AsNoTracking()
            .SingleAsync(r => r.AnonymousSessionId == sessionId);
        Assert.Equal(_someOtherUserId, owned.UserId);
    }

    [Fact]
    public async Task Merge_WithNoMatchingRows_ReturnsZero()
    {
        var connectionString = _fixture.ConnectionString;
        await using var db = new AppDbContext(Options(connectionString));
        var store = new ProductViewEventStore(db);
        var claimingUser = await CreateUserAsync(connectionString);

        Assert.Equal(0, await store.MergeAnonymousSessionAsync(
            Guid.NewGuid(), claimingUser, CancellationToken.None));
    }
}
