using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Api.Contracts.Catalog;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests.Endpoints;

public sealed class CatalogEndpointsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public CatalogEndpointsTests(TestApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProducts_WithParentCategory_ReturnsProductsFromDirectChildren()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tvAndMonitor = await dbContext.Categories.SingleAsync(c => c.Slug == "tv-man-hinh");

        var client = _factory.CreateClient();
        var response = await client.GetFromJsonAsync<PaginatedResponse<ProductSummaryDto>>(
            $"/api/products?categoryId={tvAndMonitor.Id}&pageSize=100");

        Assert.Contains(response!.Data, p => p.Sku == "TV-SAM-001");
        Assert.Contains(response.Data, p => p.Sku == "MH-SAM-001");
        Assert.DoesNotContain(response.Data, p => p.Sku == "AT-JBL-001");
    }

    [Fact]
    public async Task GetProducts_WithLeafCategory_FiltersToExactLeafAndExposesCategorySlug()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tv = await dbContext.Categories.SingleAsync(c => c.Slug == "tivi");

        var client = _factory.CreateClient();
        var response = await client.GetFromJsonAsync<PaginatedResponse<ProductSummaryDto>>(
            $"/api/products?categoryId={tv.Id}&pageSize=100");

        Assert.All(response!.Data, p => Assert.Equal("TV", p.CategoryName));
        Assert.DoesNotContain(response.Data, p => p.Sku == "MH-SAM-001");

        var detail = await client.GetFromJsonAsync<ProductDetailDto>(
            $"/api/products/{response.Data[0].Id}");
        Assert.Equal("TV", detail!.CategoryName);
    }

    [Fact]
    public async Task PublicCatalog_OmitsInactiveEntities()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var inactiveCategory = new OnlineSupermarket.Domain.Catalog.Category("Inactive Cat", "inactive-cat");
        inactiveCategory.Deactivate();
        
        var inactiveBrand = new OnlineSupermarket.Domain.Catalog.Brand("Inactive Brand", "inactive-brand");
        inactiveBrand.Deactivate();

        var activeCategory = new OnlineSupermarket.Domain.Catalog.Category("Active Cat", "active-cat");
        var activeBrand = new OnlineSupermarket.Domain.Catalog.Brand("Active Brand", "active-brand");
        
        dbContext.Categories.AddRange(inactiveCategory, activeCategory);
        dbContext.Brands.AddRange(inactiveBrand, activeBrand);
        await dbContext.SaveChangesAsync();

        var inactiveProduct = new OnlineSupermarket.Domain.Catalog.Product(activeCategory.Id, activeBrand.Id, "INAC-PROD", "Inactive Product", "inactive-product", null, 1000m, "cái", null);
        inactiveProduct.Deactivate();
        
        var productWithInactiveCategory = new OnlineSupermarket.Domain.Catalog.Product(inactiveCategory.Id, activeBrand.Id, "INAC-CAT-PROD", "Prod Inac Cat", "prod-inac-cat", null, 1000m, "cái", null);
        var productWithInactiveBrand = new OnlineSupermarket.Domain.Catalog.Product(activeCategory.Id, inactiveBrand.Id, "INAC-BRAND-PROD", "Prod Inac Brand", "prod-inac-brand", null, 1000m, "cái", null);
        
        dbContext.Products.AddRange(inactiveProduct, productWithInactiveCategory, productWithInactiveBrand);
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();

        var categoriesResponse = await client.GetFromJsonAsync<IEnumerable<CategoryDto>>("/api/categories");
        Assert.DoesNotContain(categoriesResponse!, c => c.Slug == "inactive-cat");

        var brandsResponse = await client.GetFromJsonAsync<IEnumerable<BrandDto>>("/api/brands");
        Assert.DoesNotContain(brandsResponse!, b => b.Slug == "inactive-brand");

        var productsResponse = await client.GetFromJsonAsync<PaginatedResponse<ProductSummaryDto>>("/api/products");
        Assert.DoesNotContain(productsResponse!.Data, p => p.Sku == "INAC-PROD");
        Assert.DoesNotContain(productsResponse.Data, p => p.Sku == "INAC-CAT-PROD");
        Assert.DoesNotContain(productsResponse.Data, p => p.Sku == "INAC-BRAND-PROD");

        var inactiveProdResponse = await client.GetAsync($"/api/products/{inactiveProduct.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, inactiveProdResponse.StatusCode);

        var inacCatProdResponse = await client.GetAsync($"/api/products/{productWithInactiveCategory.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, inacCatProdResponse.StatusCode);

        var inacBrandProdResponse = await client.GetAsync($"/api/products/{productWithInactiveBrand.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, inacBrandProdResponse.StatusCode);
    }

    [Fact]
    public async Task PublicCatalog_MultiLevelCategoryHierarchy_HidesDescendantsWhenRootAncestorInactive()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rootInactive = new OnlineSupermarket.Domain.Catalog.Category("Root Inactive", "root-inactive-1");
        rootInactive.Deactivate();

        var childActive = new OnlineSupermarket.Domain.Catalog.Category("Child Active", "child-active-1", rootInactive.Id);
        var leafActive = new OnlineSupermarket.Domain.Catalog.Category("Leaf Active", "leaf-active-1", childActive.Id);
        var activeBrand = new OnlineSupermarket.Domain.Catalog.Brand("Brand Tree", "brand-tree-1");

        dbContext.Categories.AddRange(rootInactive, childActive, leafActive);
        dbContext.Brands.Add(activeBrand);
        await dbContext.SaveChangesAsync();

        var product = new OnlineSupermarket.Domain.Catalog.Product(
            leafActive.Id, activeBrand.Id, "TREE-PROD-1", "Tree Product 1", "tree-product-1", null, 50000m, "cái", null);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();

        var categories = await client.GetFromJsonAsync<IEnumerable<CategoryDto>>("/api/categories");
        Assert.DoesNotContain(categories!, c => c.Slug == "root-inactive-1");
        Assert.DoesNotContain(categories!, c => c.Slug == "child-active-1");
        Assert.DoesNotContain(categories!, c => c.Slug == "leaf-active-1");

        var products = await client.GetFromJsonAsync<PaginatedResponse<ProductSummaryDto>>("/api/products");
        Assert.DoesNotContain(products!.Data, p => p.Sku == "TREE-PROD-1");

        var detailResponse = await client.GetAsync($"/api/products/{product.Id}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, detailResponse.StatusCode);

        var queryByLeaf = await client.GetFromJsonAsync<PaginatedResponse<ProductSummaryDto>>($"/api/products?categoryId={leafActive.Id}");
        Assert.Empty(queryByLeaf!.Data);
    }

    [Fact]
    public async Task PublicCatalog_MultiLevelCategoryHierarchy_WhenMiddleAncestorInactive_HidesMiddleAndLeaf()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rootActive = new OnlineSupermarket.Domain.Catalog.Category("Root Active", "root-active-2");
        var middleInactive = new OnlineSupermarket.Domain.Catalog.Category("Middle Inactive", "middle-inactive-2", rootActive.Id);
        middleInactive.Deactivate();
        var leafActive = new OnlineSupermarket.Domain.Catalog.Category("Leaf Active Under Inactive", "leaf-active-2", middleInactive.Id);
        var activeBrand = new OnlineSupermarket.Domain.Catalog.Brand("Brand Tree 2", "brand-tree-2");

        dbContext.Categories.AddRange(rootActive, middleInactive, leafActive);
        dbContext.Brands.Add(activeBrand);
        await dbContext.SaveChangesAsync();

        var product = new OnlineSupermarket.Domain.Catalog.Product(
            leafActive.Id, activeBrand.Id, "TREE-PROD-2", "Tree Product 2", "tree-product-2", null, 50000m, "cái", null);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();

        var categories = await client.GetFromJsonAsync<IEnumerable<CategoryDto>>("/api/categories");
        Assert.Contains(categories!, c => c.Slug == "root-active-2");
        Assert.DoesNotContain(categories!, c => c.Slug == "middle-inactive-2");
        Assert.DoesNotContain(categories!, c => c.Slug == "leaf-active-2");

        var products = await client.GetFromJsonAsync<PaginatedResponse<ProductSummaryDto>>("/api/products");
        Assert.DoesNotContain(products!.Data, p => p.Sku == "TREE-PROD-2");
    }

    [Fact]
    public async Task PublicCatalog_MultiLevelCategoryFilter_ReturnsProductsAcrossAllDescendantLevels()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var root = new OnlineSupermarket.Domain.Catalog.Category("Root Multi", "root-multi-3");
        var child = new OnlineSupermarket.Domain.Catalog.Category("Child Multi", "child-multi-3", root.Id);
        var subchild = new OnlineSupermarket.Domain.Catalog.Category("SubChild Multi", "subchild-multi-3", child.Id);
        var brand = new OnlineSupermarket.Domain.Catalog.Brand("Brand Multi", "brand-multi-3");

        dbContext.Categories.AddRange(root, child, subchild);
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var pRoot = new OnlineSupermarket.Domain.Catalog.Product(root.Id, brand.Id, "MULTI-P1", "Multi P1", "multi-p1", null, 1000m, "cái", null);
        var pChild = new OnlineSupermarket.Domain.Catalog.Product(child.Id, brand.Id, "MULTI-P2", "Multi P2", "multi-p2", null, 2000m, "cái", null);
        var pSub = new OnlineSupermarket.Domain.Catalog.Product(subchild.Id, brand.Id, "MULTI-P3", "Multi P3", "multi-p3", null, 3000m, "cái", null);

        dbContext.Products.AddRange(pRoot, pChild, pSub);
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();

        var response = await client.GetFromJsonAsync<PaginatedResponse<ProductSummaryDto>>(
            $"/api/products?categoryId={root.Id}&pageSize=100");

        Assert.Contains(response!.Data, p => p.Sku == "MULTI-P1");
        Assert.Contains(response.Data, p => p.Sku == "MULTI-P2");
        Assert.Contains(response.Data, p => p.Sku == "MULTI-P3");
    }

    [Fact]
    public async Task GetProducts_WithExactSkuSearch_ReturnsOnlyMatchingProduct()
    {
        var client = _factory.CreateClient();

        // Search with exact SKU 'TV-SAM-001'
        var response = await client.GetFromJsonAsync<PaginatedResponse<ProductSummaryDto>>(
            "/api/products?search=TV-SAM-001&pageSize=10");

        Assert.NotNull(response);
        Assert.Single(response.Data);
        Assert.Equal("TV-SAM-001", response.Data[0].Sku);
        Assert.Equal(1, response.Meta.TotalCount);

        // Case-insensitive exact match
        var responseLower = await client.GetFromJsonAsync<PaginatedResponse<ProductSummaryDto>>(
            "/api/products?search=tv-sam-001&pageSize=10");

        Assert.NotNull(responseLower);
        Assert.Single(responseLower.Data);
        Assert.Equal("TV-SAM-001", responseLower.Data[0].Sku);
        Assert.Equal(1, responseLower.Meta.TotalCount);
    }

    [Fact]
    public async Task GetProducts_WithFuzzySearch_ReturnsMatchingNamesAndSkus()
    {
        var client = _factory.CreateClient();

        // Search term that is not an exact SKU (e.g. 'Samsung')
        var response = await client.GetFromJsonAsync<PaginatedResponse<ProductSummaryDto>>(
            "/api/products?search=Samsung&pageSize=100");

        Assert.NotNull(response);
        Assert.NotEmpty(response.Data);
        Assert.All(response.Data, p =>
            Assert.True(p.Name.Contains("Samsung", StringComparison.OrdinalIgnoreCase) ||
                        p.Sku.Contains("Samsung", StringComparison.OrdinalIgnoreCase)));
    }
}
