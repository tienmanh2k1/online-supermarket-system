using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Api.Contracts.Admin;
using OnlineSupermarket.Api.Contracts.Catalog;
using OnlineSupermarket.Api.Tests.Auth;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests.Endpoints;

public sealed class AdminCatalogEndpointsTests : IClassFixture<AuthTestApiFactory>
{
    private readonly AuthTestApiFactory _factory;

    public AdminCatalogEndpointsTests(AuthTestApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(UserRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<OnlineSupermarket.Infrastructure.Identity.IPasswordHasher>();

        var email = $"{Guid.NewGuid()}@example.com";
        var user = User.Create(email, passwordHasher.HashPassword("Password123!"), "Test User", null, role);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var tokenService = scope.ServiceProvider.GetRequiredService<OnlineSupermarket.Infrastructure.Identity.ITokenService>();
        var token = tokenService.GenerateAccessToken(user);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ==========================================
    // AUTHENTICATION & AUTHORIZATION
    // ==========================================

    [Theory]
    [InlineData("/api/admin/catalog/categories")]
    [InlineData("/api/admin/catalog/brands")]
    [InlineData("/api/admin/catalog/products")]
    public async Task AdminCatalog_WithoutToken_ReturnsUnauthorized(string path)
    {
        using var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/catalog/categories")]
    [InlineData("/api/admin/catalog/brands")]
    [InlineData("/api/admin/catalog/products")]
    public async Task AdminCatalog_WithCustomerToken_ReturnsForbidden(string path)
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Customer);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }

    // ==========================================
    // CATEGORIES
    // ==========================================

    [Fact]
    public async Task GetCategories_ReturnsOrderedCategories()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.GetAsync("/api/admin/catalog/categories");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = await response.Content.ReadFromJsonAsync<List<AdminCategoryDto>>();
        Assert.NotNull(categories);
    }

    [Fact]
    public async Task CreateCategory_WithValidData_ReturnsCreated()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var slug = $"cat-create-{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/admin/catalog/categories",
            new UpsertCategoryRequest("Tivi mới", slug, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AdminCategoryDto>();
        Assert.NotNull(dto);
        Assert.Equal("Tivi mới", dto!.Name);
        Assert.Equal(slug, dto.Slug);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task CreateCategory_WithDuplicateSlugIgnoringCase_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slug = $"dup-cat-{Guid.NewGuid():N}";
        var existing = new Category("Tivi", slug);
        dbContext.Categories.Add(existing);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PostAsJsonAsync("/api/admin/catalog/categories",
            new UpsertCategoryRequest("Tên khác", slug.ToUpper(), null));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithNonExistentParent_ReturnsBadRequest()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PostAsJsonAsync("/api/admin/catalog/categories",
            new UpsertCategoryRequest("Tivi", $"cat-{Guid.NewGuid():N}", Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_WithInvalidInputs_ReturnsBadRequest()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PostAsJsonAsync("/api/admin/catalog/categories",
            new UpsertCategoryRequest("  ", "slug-invalid", null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_WithValidData_ReturnsOk()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Tivi Cũ", $"cat-upd-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var newSlug = $"cat-upd-new-{Guid.NewGuid():N}";
        var response = await client.PutAsJsonAsync($"/api/admin/catalog/categories/{cat.Id}",
            new UpsertCategoryRequest("Tivi Mới", newSlug, null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AdminCategoryDto>();
        Assert.NotNull(dto);
        Assert.Equal("Tivi Mới", dto!.Name);
        Assert.Equal(newSlug, dto.Slug);
    }

    [Fact]
    public async Task UpdateCategory_WithSelfParent_ReturnsBadRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = new Category("Tivi", $"tivi-self-{Guid.NewGuid():N}");
        dbContext.Categories.Add(existing);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PutAsJsonAsync($"/api/admin/catalog/categories/{existing.Id}",
            new UpsertCategoryRequest("Tivi", existing.Slug, existing.Id));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_WithMultiLevelCycle_ReturnsBadRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var catA = new Category("Cat A", $"cat-a-{Guid.NewGuid():N}");
        var catB = new Category("Cat B", $"cat-b-{Guid.NewGuid():N}", catA.Id);
        var catC = new Category("Cat C", $"cat-c-{Guid.NewGuid():N}", catB.Id);
        dbContext.Categories.AddRange(catA, catB, catC);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        // Updating catA to have catC as parent creates cycle: A -> C -> B -> A
        var response = await client.PutAsJsonAsync($"/api/admin/catalog/categories/{catA.Id}",
            new UpsertCategoryRequest(catA.Name, catA.Slug, catC.Id));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_WithDuplicateSlug_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat1 = new Category("Cat 1", $"cat-dup-1-{Guid.NewGuid():N}");
        var cat2 = new Category("Cat 2", $"cat-dup-2-{Guid.NewGuid():N}");
        dbContext.Categories.AddRange(cat1, cat2);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PutAsJsonAsync($"/api/admin/catalog/categories/{cat2.Id}",
            new UpsertCategoryRequest("Cat 2 New", cat1.Slug.ToUpper(), null));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_NotFound_ReturnsNotFound()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PutAsJsonAsync($"/api/admin/catalog/categories/{Guid.NewGuid()}",
            new UpsertCategoryRequest("Cat", "cat-nf", null));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_WithInvalidInputs_ReturnsBadRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat", $"cat-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PutAsJsonAsync($"/api/admin/catalog/categories/{cat.Id}",
            new UpsertCategoryRequest("   ", cat.Slug, null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchCategoryStatus_Deactivate_WithActiveChildren_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var parent = new Category("Parent Cat", $"parent-deact-{Guid.NewGuid():N}");
        var child = new Category("Child Cat", $"child-deact-{Guid.NewGuid():N}", parent.Id);
        dbContext.Categories.AddRange(parent, child);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PatchAsJsonAsync($"/api/admin/catalog/categories/{parent.Id}/status",
            new UpdateCatalogStatusRequest(false));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PatchCategoryStatus_Deactivate_WithActiveProducts_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat For Prod", $"cat-prod-deact-{Guid.NewGuid():N}");
        var brand = new Brand("Brand For Prod", $"brand-prod-deact-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var prod = new Product(cat.Id, brand.Id, $"SKU-DEACT-{Guid.NewGuid():N}", "Prod", "prod-deact-cat", null, 100m, "cái", null);
        dbContext.Products.Add(prod);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PatchAsJsonAsync($"/api/admin/catalog/categories/{cat.Id}/status",
            new UpdateCatalogStatusRequest(false));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PatchCategoryStatus_DeactivateAndActivate_ReturnsOk()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Solo Cat", $"solo-cat-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var deactRes = await client.PatchAsJsonAsync($"/api/admin/catalog/categories/{cat.Id}/status",
            new UpdateCatalogStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, deactRes.StatusCode);
        var deactDto = await deactRes.Content.ReadFromJsonAsync<AdminCategoryDto>();
        Assert.False(deactDto!.IsActive);

        var actRes = await client.PatchAsJsonAsync($"/api/admin/catalog/categories/{cat.Id}/status",
            new UpdateCatalogStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, actRes.StatusCode);
        var actDto = await actRes.Content.ReadFromJsonAsync<AdminCategoryDto>();
        Assert.True(actDto!.IsActive);
    }

    // ==========================================
    // BRANDS
    // ==========================================

    [Fact]
    public async Task GetBrands_ReturnsOrderedBrands()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.GetAsync("/api/admin/catalog/brands");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var brands = await response.Content.ReadFromJsonAsync<List<AdminBrandDto>>();
        Assert.NotNull(brands);
    }

    [Fact]
    public async Task CreateBrand_WithValidData_ReturnsCreated()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var slug = $"brand-create-{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/admin/catalog/brands",
            new UpsertBrandRequest("Sony", slug));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AdminBrandDto>();
        Assert.NotNull(dto);
        Assert.Equal("Sony", dto!.Name);
        Assert.Equal(slug, dto.Slug);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task CreateBrand_WithDuplicateSlugIgnoringCase_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slug = $"brand-dup-{Guid.NewGuid():N}";
        var existing = new Brand("Brand 1", slug);
        dbContext.Brands.Add(existing);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PostAsJsonAsync("/api/admin/catalog/brands",
            new UpsertBrandRequest("Brand 2", slug.ToUpper()));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateBrand_WithInvalidInputs_ReturnsBadRequest()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PostAsJsonAsync("/api/admin/catalog/brands",
            new UpsertBrandRequest("  ", "brand-slug"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBrand_WithValidData_ReturnsOk()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var brand = new Brand("Brand Old", $"brand-upd-{Guid.NewGuid():N}");
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var newSlug = $"brand-upd-new-{Guid.NewGuid():N}";
        var response = await client.PutAsJsonAsync($"/api/admin/catalog/brands/{brand.Id}",
            new UpsertBrandRequest("Brand New", newSlug));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AdminBrandDto>();
        Assert.NotNull(dto);
        Assert.Equal("Brand New", dto!.Name);
        Assert.Equal(newSlug, dto.Slug);
    }

    [Fact]
    public async Task UpdateBrand_WithDuplicateSlug_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var b1 = new Brand("B1", $"b1-dup-{Guid.NewGuid():N}");
        var b2 = new Brand("B2", $"b2-dup-{Guid.NewGuid():N}");
        dbContext.Brands.AddRange(b1, b2);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PutAsJsonAsync($"/api/admin/catalog/brands/{b2.Id}",
            new UpsertBrandRequest("B2 New", b1.Slug.ToUpper()));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBrand_NotFound_ReturnsNotFound()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PutAsJsonAsync($"/api/admin/catalog/brands/{Guid.NewGuid()}",
            new UpsertBrandRequest("B", "b-nf"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchBrandStatus_Deactivate_WithActiveProducts_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat For Brand Test", $"cat-b-deact-{Guid.NewGuid():N}");
        var brand = new Brand("Brand Deact Test", $"brand-deact-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var prod = new Product(cat.Id, brand.Id, $"SKU-B-DEACT-{Guid.NewGuid():N}", "Prod", "prod-b-deact", null, 100m, "cái", null);
        dbContext.Products.Add(prod);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PatchAsJsonAsync($"/api/admin/catalog/brands/{brand.Id}/status",
            new UpdateCatalogStatusRequest(false));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PatchBrandStatus_DeactivateAndActivate_ReturnsOk()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var brand = new Brand("Solo Brand", $"solo-brand-{Guid.NewGuid():N}");
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var deactRes = await client.PatchAsJsonAsync($"/api/admin/catalog/brands/{brand.Id}/status",
            new UpdateCatalogStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, deactRes.StatusCode);
        var deactDto = await deactRes.Content.ReadFromJsonAsync<AdminBrandDto>();
        Assert.False(deactDto!.IsActive);

        var actRes = await client.PatchAsJsonAsync($"/api/admin/catalog/brands/{brand.Id}/status",
            new UpdateCatalogStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, actRes.StatusCode);
        var actDto = await actRes.Content.ReadFromJsonAsync<AdminBrandDto>();
        Assert.True(actDto!.IsActive);
    }

    // ==========================================
    // PRODUCTS
    // ==========================================

    [Fact]
    public async Task GetProducts_WithFiltersAndPagination_ReturnsPaginatedResult()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat Filter", $"cat-filt-{Guid.NewGuid():N}");
        var brand = new Brand("Brand Filter", $"brand-filt-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var prod = new Product(cat.Id, brand.Id, $"SKU-FILT-{Guid.NewGuid():N}", "Laptop Super Pro", $"laptop-super-{Guid.NewGuid():N}", "Mô tả", 20000m, "cái", null);
        dbContext.Products.Add(prod);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.GetAsync($"/api/admin/catalog/products?search=Laptop&categoryId={cat.Id}&brandId={brand.Id}&isActive=true&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<AdminProductDto>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Data, p => p.Sku == prod.Sku);
        Assert.Equal(20, page.Meta.PageSize);
    }

    [Fact]
    public async Task CreateProduct_WithValidData_ReturnsCreated()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat Prod Valid", $"cat-pv-{Guid.NewGuid():N}");
        var brand = new Brand("Brand Prod Valid", $"brand-pv-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var sku = $"ADM-PROD-{Guid.NewGuid():N}";
        var slug = $"prod-slug-{Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/admin/catalog/products", new UpsertProductRequest(
            cat.Id, brand.Id, sku, "Sản phẩm Admin", slug,
            "Mô tả chi tiết", 1_250_000m, "cái", "https://img.example/item.jpg"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AdminProductDto>();
        Assert.NotNull(dto);
        Assert.Equal(sku, dto!.Sku);
        Assert.Equal(slug, dto.Slug);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateSku_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat Sku", $"cat-sku-{Guid.NewGuid():N}");
        var brand = new Brand("Brand Sku", $"brand-sku-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var sku = $"SKU-DUP-{Guid.NewGuid():N}";
        var existing = new Product(cat.Id, brand.Id, sku, "Existing Prod", $"prod-ex-{Guid.NewGuid():N}", null, 1000m, "cái", null);
        dbContext.Products.Add(existing);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PostAsJsonAsync("/api/admin/catalog/products", new UpsertProductRequest(
            cat.Id, brand.Id, sku.ToLower(), "Different Name", $"prod-diff-{Guid.NewGuid():N}", null, 2000m, "cái", null));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateSlug_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat Slug", $"cat-slug-{Guid.NewGuid():N}");
        var brand = new Brand("Brand Slug", $"brand-slug-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var slug = $"slug-dup-{Guid.NewGuid():N}";
        var existing = new Product(cat.Id, brand.Id, $"SKU-A-{Guid.NewGuid():N}", "Prod 1", slug, null, 1000m, "cái", null);
        dbContext.Products.Add(existing);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PostAsJsonAsync("/api/admin/catalog/products", new UpsertProductRequest(
            cat.Id, brand.Id, $"SKU-B-{Guid.NewGuid():N}", "Prod 2", slug.ToUpper(), null, 2000m, "cái", null));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithInactiveCategory_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat Inactive", $"cat-inac-{Guid.NewGuid():N}");
        cat.Deactivate();
        var brand = new Brand("Brand Active", $"brand-act-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PostAsJsonAsync("/api/admin/catalog/products", new UpsertProductRequest(
            cat.Id, brand.Id, $"SKU-INAC-{Guid.NewGuid():N}", "Prod", $"prod-{Guid.NewGuid():N}", null, 1000m, "cái", null));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithInactiveBrand_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat Active", $"cat-act-{Guid.NewGuid():N}");
        var brand = new Brand("Brand Inactive", $"brand-inac-{Guid.NewGuid():N}");
        brand.Deactivate();
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PostAsJsonAsync("/api/admin/catalog/products", new UpsertProductRequest(
            cat.Id, brand.Id, $"SKU-INACB-{Guid.NewGuid():N}", "Prod", $"prod-{Guid.NewGuid():N}", null, 1000m, "cái", null));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithInvalidInputs_ReturnsBadRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat Valid", $"cat-v-{Guid.NewGuid():N}");
        var brand = new Brand("Brand Valid", $"brand-v-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PostAsJsonAsync("/api/admin/catalog/products", new UpsertProductRequest(
            cat.Id, brand.Id, "SKU", "Name", "slug", null, -100m, "cái", null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProduct_WithValidData_ReturnsOk()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat Upd", $"cat-u-{Guid.NewGuid():N}");
        var brand = new Brand("Brand Upd", $"brand-u-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var prod = new Product(cat.Id, brand.Id, $"SKU-U-{Guid.NewGuid():N}", "Old Prod", $"prod-u-{Guid.NewGuid():N}", "Old Desc", 10000m, "cái", null);
        dbContext.Products.Add(prod);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PutAsJsonAsync($"/api/admin/catalog/products/{prod.Id}", new UpsertProductRequest(
            cat.Id, brand.Id, "SKU-U-NEW", "New Prod Name", "prod-u-new", "New Desc", 20000m, "hộp", "https://img/item.png"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AdminProductDto>();
        Assert.NotNull(dto);
        Assert.Equal("SKU-U-NEW", dto!.Sku);
        Assert.Equal("New Prod Name", dto.Name);
    }

    [Fact]
    public async Task UpdateProduct_KeepingExistingInactiveCategoryAndBrand_ReturnsOk()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat Inactive Keep", $"cat-ink-{Guid.NewGuid():N}");
        var brand = new Brand("Brand Inactive Keep", $"brand-ink-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var prod = new Product(cat.Id, brand.Id, $"SKU-INK-{Guid.NewGuid():N}", "Prod Ink", $"prod-ink-{Guid.NewGuid():N}", null, 1000m, "cái", null);
        dbContext.Products.Add(prod);
        await dbContext.SaveChangesAsync();

        // Now deactivate both cat and brand
        cat.Deactivate();
        brand.Deactivate();
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        // Update product description / price keeping same categoryId & brandId
        var response = await client.PutAsJsonAsync($"/api/admin/catalog/products/{prod.Id}", new UpsertProductRequest(
            cat.Id, brand.Id, prod.Sku, "Prod Ink Updated", prod.Slug, "Updated description", 1500m, "cái", null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProduct_ChangingToInactiveCategory_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat1 = new Category("Cat 1", $"cat-c1-{Guid.NewGuid():N}");
        var cat2 = new Category("Cat 2 Inactive", $"cat-c2-{Guid.NewGuid():N}");
        cat2.Deactivate();
        var brand = new Brand("Brand", $"brand-c-{Guid.NewGuid():N}");
        dbContext.Categories.AddRange(cat1, cat2);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var prod = new Product(cat1.Id, brand.Id, $"SKU-CHG-{Guid.NewGuid():N}", "Prod", $"prod-chg-{Guid.NewGuid():N}", null, 1000m, "cái", null);
        dbContext.Products.Add(prod);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PutAsJsonAsync($"/api/admin/catalog/products/{prod.Id}", new UpsertProductRequest(
            cat2.Id, brand.Id, prod.Sku, prod.Name, prod.Slug, null, prod.BasePrice, prod.Unit, null));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PatchProductStatus_DeactivateAndActivate_ReturnsOk()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat Act", $"cat-act-{Guid.NewGuid():N}");
        var brand = new Brand("Brand Act", $"brand-act-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var prod = new Product(cat.Id, brand.Id, $"SKU-STATUS-{Guid.NewGuid():N}", "Prod", $"prod-st-{Guid.NewGuid():N}", null, 1000m, "cái", null);
        dbContext.Products.Add(prod);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var deactRes = await client.PatchAsJsonAsync($"/api/admin/catalog/products/{prod.Id}/status",
            new UpdateCatalogStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, deactRes.StatusCode);
        var deactDto = await deactRes.Content.ReadFromJsonAsync<AdminProductDto>();
        Assert.False(deactDto!.IsActive);

        var actRes = await client.PatchAsJsonAsync($"/api/admin/catalog/products/{prod.Id}/status",
            new UpdateCatalogStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, actRes.StatusCode);
        var actDto = await actRes.Content.ReadFromJsonAsync<AdminProductDto>();
        Assert.True(actDto!.IsActive);
    }

    [Fact]
    public async Task PatchProductStatus_Activate_WithInactiveCategoryOrBrand_ReturnsConflict()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat For Restore", $"cat-res-{Guid.NewGuid():N}");
        var brand = new Brand("Brand For Restore", $"brand-res-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var prod = new Product(cat.Id, brand.Id, $"SKU-RESTORE-{Guid.NewGuid():N}", "Prod", $"prod-res-{Guid.NewGuid():N}", null, 1000m, "cái", null);
        prod.Deactivate();
        cat.Deactivate(); // Category is now inactive
        dbContext.Products.Add(prod);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PatchAsJsonAsync($"/api/admin/catalog/products/{prod.Id}/status",
            new UpdateCatalogStatusRequest(true));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(null, "slug-valid")]
    [InlineData("", "slug-valid")]
    [InlineData("   ", "slug-valid")]
    [InlineData("Valid Name", null)]
    [InlineData("Valid Name", "")]
    [InlineData("Valid Name", "   ")]
    public async Task CreateCategory_WithNullOrWhitespaceFields_ReturnsBadRequest(string? name, string? slug)
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PostAsJsonAsync("/api/admin/catalog/categories", new UpsertCategoryRequest(name!, slug!, null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null, "slug-valid")]
    [InlineData("", "slug-valid")]
    [InlineData("Valid Name", null)]
    [InlineData("Valid Name", "")]
    public async Task UpdateCategory_WithNullOrWhitespaceFields_ReturnsBadRequest(string? name, string? slug)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat", $"cat-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PutAsJsonAsync($"/api/admin/catalog/categories/{cat.Id}", new UpsertCategoryRequest(name!, slug!, null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null, "slug-valid")]
    [InlineData("", "slug-valid")]
    [InlineData("Valid Name", null)]
    [InlineData("Valid Name", "")]
    public async Task CreateBrand_WithNullOrWhitespaceFields_ReturnsBadRequest(string? name, string? slug)
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PostAsJsonAsync("/api/admin/catalog/brands", new UpsertBrandRequest(name!, slug!));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null, "slug-valid")]
    [InlineData("", "slug-valid")]
    [InlineData("Valid Name", null)]
    [InlineData("Valid Name", "")]
    public async Task UpdateBrand_WithNullOrWhitespaceFields_ReturnsBadRequest(string? name, string? slug)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var brand = new Brand("Brand", $"brand-{Guid.NewGuid():N}");
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.PutAsJsonAsync($"/api/admin/catalog/brands/{brand.Id}", new UpsertBrandRequest(name!, slug!));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_WithNullOrMissingRequiredFields_ReturnsBadRequest()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);

        // Name null
        var r1 = await client.PostAsJsonAsync("/api/admin/catalog/products", new UpsertProductRequest(
            Guid.NewGuid(), Guid.NewGuid(), "SKU1", null!, "slug1", null, 100m, "cái", null));
        Assert.Equal(HttpStatusCode.BadRequest, r1.StatusCode);

        // Slug null
        var r2 = await client.PostAsJsonAsync("/api/admin/catalog/products", new UpsertProductRequest(
            Guid.NewGuid(), Guid.NewGuid(), "SKU2", "Name2", null!, null, 100m, "cái", null));
        Assert.Equal(HttpStatusCode.BadRequest, r2.StatusCode);

        // SKU null
        var r3 = await client.PostAsJsonAsync("/api/admin/catalog/products", new UpsertProductRequest(
            Guid.NewGuid(), Guid.NewGuid(), null!, "Name3", "slug3", null, 100m, "cái", null));
        Assert.Equal(HttpStatusCode.BadRequest, r3.StatusCode);

        // Unit null
        var r4 = await client.PostAsJsonAsync("/api/admin/catalog/products", new UpsertProductRequest(
            Guid.NewGuid(), Guid.NewGuid(), "SKU4", "Name4", "slug4", null, 100m, null!, null));
        Assert.Equal(HttpStatusCode.BadRequest, r4.StatusCode);

        // Empty CategoryId
        var r5 = await client.PostAsJsonAsync("/api/admin/catalog/products", new UpsertProductRequest(
            Guid.Empty, Guid.NewGuid(), "SKU5", "Name5", "slug5", null, 100m, "cái", null));
        Assert.Equal(HttpStatusCode.BadRequest, r5.StatusCode);

        // Empty BrandId
        var r6 = await client.PostAsJsonAsync("/api/admin/catalog/products", new UpsertProductRequest(
            Guid.NewGuid(), Guid.Empty, "SKU6", "Name6", "slug6", null, 100m, "cái", null));
        Assert.Equal(HttpStatusCode.BadRequest, r6.StatusCode);
    }

    [Fact]
    public async Task UpdateProduct_WithNullOrMissingRequiredFields_ReturnsBadRequest()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cat = new Category("Cat", $"cat-{Guid.NewGuid():N}");
        var brand = new Brand("Brand", $"brand-{Guid.NewGuid():N}");
        dbContext.Categories.Add(cat);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var prod = new Product(cat.Id, brand.Id, $"SKU-NULL-{Guid.NewGuid():N}", "Prod", $"prod-null-{Guid.NewGuid():N}", null, 1000m, "cái", null);
        dbContext.Products.Add(prod);
        await dbContext.SaveChangesAsync();

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);

        var r1 = await client.PutAsJsonAsync($"/api/admin/catalog/products/{prod.Id}", new UpsertProductRequest(
            cat.Id, brand.Id, "SKU", null!, "slug", null, 100m, "cái", null));
        Assert.Equal(HttpStatusCode.BadRequest, r1.StatusCode);

        var r2 = await client.PutAsJsonAsync($"/api/admin/catalog/products/{prod.Id}", new UpsertProductRequest(
            cat.Id, brand.Id, "SKU", "Name", null!, null, 100m, "cái", null));
        Assert.Equal(HttpStatusCode.BadRequest, r2.StatusCode);
    }
}
