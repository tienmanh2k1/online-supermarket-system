using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Api.Contracts.Review;
using OnlineSupermarket.Api.Endpoints;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Reviews;
using OnlineSupermarket.Infrastructure.Identity;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests.Endpoints;

public class ReviewEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public ReviewEndpointsTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateReview_WhenUnauthorized_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            orderItemId = Guid.NewGuid(),
            rating = 5,
            comment = "Tốt"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task CreateReview_WhenRatingOutOfRange_ReturnsBadRequest(int invalidRating)
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: true);
        using var client = await CreateCustomerClientAsync(fixture.UserId);

        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            orderItemId = fixture.OrderItemId,
            rating = invalidRating,
            comment = "Không hợp lệ"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateReview_WhenOrderNotCompleted_ReturnsConflict()
    {
        var fixture = await SeedOrderItemAsync("Pending", ownedByCaller: true);
        using var client = await CreateCustomerClientAsync(fixture.UserId);

        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            orderItemId = fixture.OrderItemId,
            rating = 5,
            comment = "Hàng tốt"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateReview_WhenCallerIsNotOrderOwner_ReturnsForbidden()
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: false);
        using var client = await CreateCustomerClientAsync();

        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            orderItemId = fixture.OrderItemId,
            rating = 5,
            comment = "Đánh giá trộm"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateReview_WhenEligible_ReturnsCreatedAndPersistsReview()
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: true);
        using var client = await CreateCustomerClientAsync(fixture.UserId);

        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            orderItemId = fixture.OrderItemId,
            rating = 5,
            comment = "Sản phẩm rất tốt"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ReviewDto>();
        Assert.NotNull(dto);
        Assert.Equal(fixture.ProductId, dto.ProductId);
        Assert.Equal(5, dto.Rating);
        Assert.Equal("Sản phẩm rất tốt", dto.Comment);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await dbContext.Reviews.FirstOrDefaultAsync(r => r.OrderItemId == fixture.OrderItemId);
        Assert.NotNull(persisted);
        Assert.Equal(5, persisted.Rating);
    }

    [Fact]
    public async Task CreateReview_WhenAlreadyReviewed_ReturnsConflict()
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: true);
        using var client = await CreateCustomerClientAsync(fixture.UserId);

        var res1 = await client.PostAsJsonAsync("/api/reviews", new
        {
            orderItemId = fixture.OrderItemId,
            rating = 5,
            comment = "Đánh giá lần 1"
        });
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        var res2 = await client.PostAsJsonAsync("/api/reviews", new
        {
            orderItemId = fixture.OrderItemId,
            rating = 4,
            comment = "Đánh giá lần 2"
        });
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }

    [Fact]
    public async Task UpdateReview_WhenValid_UpdatesRatingAndComment()
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: true);
        using var client = await CreateCustomerClientAsync(fixture.UserId);

        var createRes = await client.PostAsJsonAsync("/api/reviews", new
        {
            orderItemId = fixture.OrderItemId,
            rating = 5,
            comment = "Ban đầu"
        });
        var created = await createRes.Content.ReadFromJsonAsync<ReviewDto>();
        Assert.NotNull(created);

        var updateRes = await client.PutAsJsonAsync($"/api/reviews/{created.Id}", new
        {
            rating = 3,
            comment = "Sửa lại thành 3 sao"
        });

        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await updateRes.Content.ReadFromJsonAsync<ReviewDto>();
        Assert.NotNull(updated);
        Assert.Equal(3, updated.Rating);
        Assert.Equal("Sửa lại thành 3 sao", updated.Comment);
    }

    [Fact]
    public async Task GetProductReviews_ReturnsAggregatesAndPaginatedList()
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: true);
        using var client = await CreateCustomerClientAsync(fixture.UserId);

        await client.PostAsJsonAsync("/api/reviews", new
        {
            orderItemId = fixture.OrderItemId,
            rating = 5,
            comment = "Rất ngon"
        });

        using var publicClient = _factory.CreateClient();
        var response = await publicClient.GetAsync($"/api/products/{fixture.ProductId}/reviews?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProductReviewsDto>();
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(5.0m, result.AverageRating);
        Assert.Single(result.Data);
        Assert.Equal("Rất ngon", result.Data[0].Comment);
    }

    [Fact]
    public async Task GetReviewEligibility_WhenHasCompletedPurchasesWithoutReview_ReturnsCanReviewTrue()
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: true);
        using var client = await CreateCustomerClientAsync(fixture.UserId);

        var response = await client.GetAsync($"/api/products/{fixture.ProductId}/review-eligibility");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ReviewEligibilityDto>();
        Assert.NotNull(result);
        Assert.True(result.CanReview);
        Assert.Equal(fixture.OrderItemId, result.OrderItemId);
        Assert.Null(result.ReviewId);
    }

    [Fact]
    public async Task GetReviewEligibility_WhenUserHasAlreadyReviewed_ReturnsCanReviewFalseWithExistingDetails()
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: true);
        using var client = await CreateCustomerClientAsync(fixture.UserId);

        var createRes = await client.PostAsJsonAsync("/api/reviews", new
        {
            orderItemId = fixture.OrderItemId,
            rating = 5,
            comment = "Tốt"
        });
        var created = await createRes.Content.ReadFromJsonAsync<ReviewDto>();
        Assert.NotNull(created);

        var response = await client.GetAsync($"/api/products/{fixture.ProductId}/review-eligibility");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ReviewEligibilityDto>();
        Assert.NotNull(result);
        Assert.False(result.CanReview);
        Assert.Null(result.OrderItemId);
        Assert.Equal(created.Id, result.ReviewId);
        Assert.Equal(5, result.ExistingRating);
        Assert.Equal("Tốt", result.ExistingComment);
    }

    [Fact]
    public async Task CreateReview_WhenProductIsInactive_ReturnsNotFound()
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: true, isProductActive: false);
        using var client = await CreateCustomerClientAsync(fixture.UserId);

        var response = await client.PostAsJsonAsync("/api/reviews", new
        {
            orderItemId = fixture.OrderItemId,
            rating = 5,
            comment = "Tốt"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProductReviews_WhenProductIsInactive_ReturnsNotFound()
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: true, isProductActive: false);
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/products/{fixture.ProductId}/reviews");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetReviewEligibility_WhenProductIsInactive_ReturnsNotFound()
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: true, isProductActive: false);
        using var client = await CreateCustomerClientAsync(fixture.UserId);

        var response = await client.GetAsync($"/api/products/{fixture.ProductId}/review-eligibility");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetReviewEligibility_WithTarget_ValidItem_ReturnsCanReviewTrueForThatItem()
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: true);
        using var client = await CreateCustomerClientAsync(fixture.UserId);

        var response = await client.GetAsync($"/api/products/{fixture.ProductId}/review-eligibility?orderItemId={fixture.OrderItemId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ReviewEligibilityDto>();
        Assert.NotNull(result);
        Assert.True(result.CanReview);
        Assert.Equal(fixture.OrderItemId, result.OrderItemId);
    }

    [Fact]
    public async Task GetReviewEligibility_WithTarget_ProductMismatch_Returns400()
    {
        var firstFixture = await SeedOrderItemAsync("Completed", ownedByCaller: true);
        var secondFixture = await SeedOrderItemAsync("Completed", ownedByCaller: true, callerUserId: firstFixture.UserId);
        using var client = await CreateCustomerClientAsync(firstFixture.UserId);

        // orderItemId belongs to firstFixture.ProductId, but the query uses secondFixture.ProductId.
        var response = await client.GetAsync($"/api/products/{secondFixture.ProductId}/review-eligibility?orderItemId={firstFixture.OrderItemId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ORDER_ITEM_PRODUCT_MISMATCH", body);
    }

    [Fact]
    public async Task GetReviewEligibility_WithTarget_NotOwnedByCaller_ReturnsCanReviewFalse()
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: false);
        using var client = await CreateCustomerClientAsync(fixture.UserId);

        var response = await client.GetAsync($"/api/products/{fixture.ProductId}/review-eligibility?orderItemId={fixture.OrderItemId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ReviewEligibilityDto>();
        Assert.NotNull(result);
        Assert.False(result.CanReview);
        Assert.Null(result.OrderItemId);
    }

    [Fact]
    public async Task GetReviewEligibility_WithTarget_AlreadyReviewed_ReturnsCanReviewFalseWithoutItem()
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: true);
        using var client = await CreateCustomerClientAsync(fixture.UserId);

        var createRes = await client.PostAsJsonAsync("/api/reviews", new
        {
            orderItemId = fixture.OrderItemId,
            rating = 4,
            comment = "Đã đánh giá"
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var response = await client.GetAsync($"/api/products/{fixture.ProductId}/review-eligibility?orderItemId={fixture.OrderItemId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ReviewEligibilityDto>();
        Assert.NotNull(result);
        Assert.False(result.CanReview);
        Assert.Null(result.OrderItemId);
        Assert.Null(result.ReviewId);
    }

    [Fact]
    public async Task GetReviewById_WhenExists_ReturnsReviewDto()
    {
        var fixture = await SeedOrderItemAsync("Completed", ownedByCaller: true);
        using var client = await CreateCustomerClientAsync(fixture.UserId);

        var createRes = await client.PostAsJsonAsync("/api/reviews", new
        {
            orderItemId = fixture.OrderItemId,
            rating = 4,
            comment = "Khá hài lòng"
        });
        var created = await createRes.Content.ReadFromJsonAsync<ReviewDto>();
        Assert.NotNull(created);

        using var publicClient = _factory.CreateClient();
        var response = await publicClient.GetAsync($"/api/reviews/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<ReviewDto>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(4, fetched.Rating);
        Assert.Equal("Khá hài lòng", fetched.Comment);
    }

    [Fact]
    public void IsUniqueOrderItemViolation_OnlyMatchesDuplicateOnOrderItemId()
    {
        // 1. Matches duplicate entry on ix_reviews_order_item_id
        var exValid1 = new DbUpdateException("Database error", new Exception("Duplicate entry 'xxx' for key 'ix_reviews_order_item_id'"));
        Assert.True(ReviewEndpoints.IsUniqueOrderItemViolation(exValid1));

        // 2. Matches error 1062 with order_item_id
        var exValid2 = new DbUpdateException("Database error", new Exception("Error 1062: Duplicate entry for column 'order_item_id'"));
        Assert.True(ReviewEndpoints.IsUniqueOrderItemViolation(exValid2));

        // 3. Does NOT match other duplicate key (e.g. user email)
        var exOtherUnique = new DbUpdateException("Database error", new Exception("Duplicate entry 'admin@test.com' for key 'users.ix_users_email'"));
        Assert.False(ReviewEndpoints.IsUniqueOrderItemViolation(exOtherUnique));

        // 4. Does NOT match error 1062 on another table
        var exOther1062 = new DbUpdateException("Database error", new Exception("Error 1062: Duplicate entry 'XYZ' for key 'promotions.ix_promotions_code'"));
        Assert.False(ReviewEndpoints.IsUniqueOrderItemViolation(exOther1062));

        // 5. Does NOT match general foreign key violation
        var exFk = new DbUpdateException("Database error", new Exception("Cannot add or update a child row: a foreign key constraint fails"));
        Assert.False(ReviewEndpoints.IsUniqueOrderItemViolation(exFk));
    }

    private async Task<HttpClient> CreateCustomerClientAsync(Guid? preferredUserId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = preferredUserId.HasValue
            ? await dbContext.Users.FirstOrDefaultAsync(u => u.Id == preferredUserId.Value)
            : null;

        if (user is null)
        {
            var uniqueEmail = $"cust_{Guid.NewGuid():N}@example.com";
            user = User.Create(
                uniqueEmail,
                passwordHasher.HashPassword("TestPassword123!"),
                "Test Customer",
                "0987654321");

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var token = tokenService.GenerateAccessToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private async Task<(Guid UserId, Guid OrderId, Guid OrderItemId, Guid ProductId)> SeedOrderItemAsync(
        string statusString,
        bool ownedByCaller,
        Guid? callerUserId = null,
        Guid? existingProductId = null,
        bool isProductActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        User user;
        if (callerUserId.HasValue)
        {
            user = await dbContext.Users.FirstAsync(u => u.Id == callerUserId.Value);
        }
        else
        {
            user = User.Create($"user_{Guid.NewGuid():N}@example.com", passwordHasher.HashPassword("Pass123!"), "Nguyen Van A", "0900000000");
            dbContext.Users.Add(user);
        }

        var ownerId = ownedByCaller ? user.Id : Guid.NewGuid();
        if (!ownedByCaller)
        {
            var otherUser = User.Create($"other_{Guid.NewGuid():N}@example.com", passwordHasher.HashPassword("Pass123!"), "Other User", "0911111111");
            dbContext.Users.Add(otherUser);
            ownerId = otherUser.Id;
        }

        Product product;
        if (existingProductId.HasValue)
        {
            product = await dbContext.Products.FirstAsync(p => p.Id == existingProductId.Value);
        }
        else
        {
            var category = new Category("Category " + Guid.NewGuid().ToString("N"), "cat-" + Guid.NewGuid().ToString("N"));
            var brand = new Brand("Brand " + Guid.NewGuid().ToString("N"), "brand-" + Guid.NewGuid().ToString("N"));
            dbContext.Categories.Add(category);
            dbContext.Brands.Add(brand);
            await dbContext.SaveChangesAsync();

            product = new Product(
                category.Id,
                brand.Id,
                "SKU-" + Guid.NewGuid().ToString("N")[..8],
                "Product Test " + Guid.NewGuid().ToString("N")[..6],
                "slug-" + Guid.NewGuid().ToString("N")[..8],
                "Test description",
                100000m,
                "cái",
                "https://example.com/img.jpg");
            dbContext.Products.Add(product);
        }

        if (!isProductActive)
        {
            product.Deactivate();
        }

        var branch = await dbContext.Branches.FirstOrDefaultAsync();
        if (branch is null)
        {
            branch = new Branch("Chi nhánh Quận 1", "123 Le Loi, Q1", "0123456789", null, null);
            dbContext.Branches.Add(branch);
        }

        await dbContext.SaveChangesAsync();

        var status = Enum.Parse<OrderStatus>(statusString);
        var order = Order.Create(
            ownerId,
            branch.Id,
            "Delivery",
            "Nguyen Van A",
            "0900000000",
            "123 Le Loi",
            null,
            [(product.Id, product.Name, product.Sku, product.BasePrice, 1, product.BasePrice)],
            product.BasePrice,
            0,
            0,
            product.BasePrice);

        if (status != OrderStatus.Pending)
        {
            order.SetStatus(status, "Status updated in test");
        }

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        return (user.Id, order.Id, order.Items[0].Id, product.Id);
    }
}
