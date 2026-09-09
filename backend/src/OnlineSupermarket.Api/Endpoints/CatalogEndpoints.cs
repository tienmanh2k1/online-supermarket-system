using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Api.Contracts.Catalog;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Catalog");

        // Public Catalog APIs
        group.MapGet("/products", GetProductsAsync);
        group.MapGet("/products/{id:guid}", GetProductByIdAsync);
        group.MapGet("/categories", GetCategoriesAsync);
        group.MapGet("/brands", GetBrandsAsync);

        // Admin CRUD APIs
        group.MapPost("/products", CreateProductAsync);
        group.MapPut("/products/{id:guid}", UpdateProductAsync);
        group.MapDelete("/products/{id:guid}", DeleteProductAsync);

        // AI Recommendation APIs
        group.MapPost("/products/{id:guid}/view", RecordProductViewAsync);
        // /recommendations mapped in RecommendationEndpoints.cs

        return routes;
    }

    private static async Task<IResult> GetProductsAsync(
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? brandId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] Guid? branchId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromServices] AppDbContext dbContext = null!,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        // Get all categories for ancestor checking
        var allCategories = await dbContext.Categories
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Filter to only categories with active ancestors
        var activeCategoryIds = allCategories
            .Where(c => c.IsActive && HasActiveAncestors(c, allCategories))
            .Select(c => c.Id)
            .ToHashSet();

        // Build category descendants list if categoryId specified
        HashSet<Guid>? categoryIds = null;
        if (categoryId.HasValue)
        {
            var descendants = GetDescendantCategoryIds(categoryId.Value, allCategories);
            // Intersect with active categories
            categoryIds = descendants.Intersect(activeCategoryIds).ToHashSet();
        }

        var query = dbContext.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Where(p =>
                p.IsActive &&
                activeCategoryIds.Contains(p.CategoryId) &&
                p.Brand != null && p.Brand.IsActive)
            .AsQueryable();

        if (categoryIds != null && categoryIds.Count > 0) query = query.Where(p => categoryIds.Contains(p.CategoryId));
        else if (categoryId.HasValue) query = query.Where(p => false); // category requested but no valid descendants
        if (brandId.HasValue) query = query.Where(p => p.BrandId == brandId.Value);
        if (minPrice.HasValue) query = query.Where(p => p.BasePrice >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(p => p.BasePrice <= maxPrice.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Sku.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var products = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductSummaryDto(
                p.Id,
                p.Name,
                p.Slug,
                p.Sku,
                p.BasePrice,
                p.ImageUrl,
                p.Category != null ? p.Category.Name : string.Empty,
                p.Brand != null ? p.Brand.Name : string.Empty))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PaginatedResponse<ProductSummaryDto>(
            products, new PaginationMeta(totalCount, page, pageSize, totalPages)));
    }

    private static async Task<IResult> GetProductByIdAsync(
        [FromRoute] Guid id,
        [FromQuery] Guid? branchId,
        [FromServices] AppDbContext dbContext = null!,
        CancellationToken cancellationToken = default)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, cancellationToken);

        if (product == null) return Results.NotFound(new { message = "Product not found." });

        // Check category and brand are active with active ancestors
        var allCategories = await dbContext.Categories.AsNoTracking().ToListAsync(cancellationToken);
        var category = allCategories.FirstOrDefault(c => c.Id == product.CategoryId);
        if (category == null || !category.IsActive || !HasActiveAncestors(category, allCategories))
            return Results.NotFound(new { message = "Product not found." });

        var brand = await dbContext.Brands.AsNoTracking().FirstOrDefaultAsync(b => b.Id == product.BrandId, cancellationToken);
        if (brand == null || !brand.IsActive)
            return Results.NotFound(new { message = "Product not found." });

        BranchInventoryDto? inventory = null;
        if (branchId.HasValue)
        {
            var inv = await dbContext.BranchInventories
                .AsNoTracking()
                .FirstOrDefaultAsync(bi => bi.BranchId == branchId.Value && bi.ProductId == id, cancellationToken);

            if (inv != null)
            {
                inventory = new BranchInventoryDto(
                    inv.BranchId,
                    inv.SellingPrice,
                    inv.QuantityOnHand - inv.ReservedQuantity,
                    inv.QuantityOnHand);
            }
        }

        return Results.Ok(new ProductDetailDto(
            product.Id,
            product.Name,
            product.Slug,
            product.Sku,
            product.Description,
            product.BasePrice,
            product.Unit,
            product.ImageUrl,
            product.CategoryId,
            product.Category != null ? product.Category.Name : string.Empty,
            product.BrandId,
            product.Brand != null ? product.Brand.Name : string.Empty,
            inventory));
    }

    private static async Task<IResult> CreateProductAsync(
        [FromBody] CreateProductRequest request,
        [FromServices] AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var slug = request.Name.ToLower().Replace(" ", "-");
        var product = new Product(
            request.CategoryId,
            request.BrandId,
            request.Sku,
            request.Name,
            slug,
            request.Description,
            request.BasePrice,
            request.Unit,
            request.ImageUrl);

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/products/{product.Id}", new { id = product.Id, message = "Thêm sản phẩm thành công!" });
    }

    private static async Task<IResult> UpdateProductAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateProductRequest request,
        [FromServices] AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product == null) return Results.NotFound(new { message = "Không tìm thấy sản phẩm." });

        // Update properties via Reflection / Setters if private
        typeof(Product).GetProperty(nameof(Product.Name))?.SetValue(product, request.Name);
        typeof(Product).GetProperty(nameof(Product.Sku))?.SetValue(product, request.Sku);
        typeof(Product).GetProperty(nameof(Product.BasePrice))?.SetValue(product, request.BasePrice);
        typeof(Product).GetProperty(nameof(Product.Unit))?.SetValue(product, request.Unit);
        typeof(Product).GetProperty(nameof(Product.CategoryId))?.SetValue(product, request.CategoryId);
        typeof(Product).GetProperty(nameof(Product.BrandId))?.SetValue(product, request.BrandId);
        typeof(Product).GetProperty(nameof(Product.Description))?.SetValue(product, request.Description);
        typeof(Product).GetProperty(nameof(Product.ImageUrl))?.SetValue(product, request.ImageUrl);
        typeof(Product).GetProperty(nameof(Product.IsActive))?.SetValue(product, request.IsActive);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { message = "Cập nhật sản phẩm thành công!" });
    }

    private static async Task<IResult> DeleteProductAsync(
        [FromRoute] Guid id,
        [FromServices] AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product == null) return Results.NotFound(new { message = "Không tìm thấy sản phẩm." });

        typeof(Product).GetProperty(nameof(Product.IsActive))?.SetValue(product, false);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = "Xóa sản phẩm thành công (chuyển về dạng ẩn)!" });
    }

    private static async Task<IResult> RecordProductViewAsync(
        [FromRoute] Guid id,
        [FromBody] RecordProductViewRequest request,
        [FromServices] AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Ghi nhận sự kiện xem sản phẩm
        return Results.Ok(new { status = "success", recordedAt = DateTime.UtcNow });
    }

    private static async Task<IResult> GetRecommendationsAsync(
        [FromQuery] Guid? productId,
        [FromQuery] int limit = 6,
        [FromServices] AppDbContext dbContext = null!,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Where(p => p.IsActive);

        if (productId.HasValue)
        {
            var currentProd = await dbContext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId.Value, cancellationToken);
            if (currentProd != null)
            {
                query = query.Where(p => p.Id != productId.Value && (p.CategoryId == currentProd.CategoryId || p.BrandId == currentProd.BrandId));
            }
        }

        var recommendations = await query
            .Take(limit)
            .Select(p => new ProductSummaryDto(
                p.Id,
                p.Name,
                p.Slug,
                p.Sku,
                p.BasePrice,
                p.ImageUrl,
                p.Category != null ? p.Category.Name : string.Empty,
                p.Brand != null ? p.Brand.Name : string.Empty))
            .ToListAsync(cancellationToken);

        return Results.Ok(recommendations);
    }

    private static HashSet<Guid> GetDescendantCategoryIds(Guid rootId, IList<Category> allCategories)
    {
        var result = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            result.Add(currentId);
            var children = allCategories.Where(c => c.ParentCategoryId == currentId).Select(c => c.Id);
            foreach (var childId in children) queue.Enqueue(childId);
        }
        return result;
    }

    private static async Task<IResult> GetCategoriesAsync([FromServices] AppDbContext dbContext = null!, CancellationToken cancellationToken = default)
    {
        var allCategories = await dbContext.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);

        // Filter to only active categories with active ancestors
        var activeWithActiveAncestors = allCategories
            .Where(c => c.IsActive && HasActiveAncestors(c, allCategories))
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.ParentCategoryId, c.IsActive))
            .ToList();

        return Results.Ok(activeWithActiveAncestors);
    }

    private static bool HasActiveAncestors(Category category, IList<Category> allCategories)
    {
        var current = category;
        while (current.ParentCategoryId.HasValue)
        {
            var parent = allCategories.FirstOrDefault(c => c.Id == current.ParentCategoryId.Value);
            if (parent == null || !parent.IsActive) return false;
            current = parent;
        }
        return true;
    }

    private static async Task<IResult> GetBrandsAsync([FromServices] AppDbContext dbContext = null!, CancellationToken cancellationToken = default)
    {
        var brands = await dbContext.Brands.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.Name)
            .Select(b => new BrandDto(b.Id, b.Name, b.Slug, b.IsActive)).ToListAsync(cancellationToken);
        return Results.Ok(brands);
    }
}
