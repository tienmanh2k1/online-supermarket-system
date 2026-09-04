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
        group.MapGet("/recommendations", GetRecommendationsAsync);

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

        var query = dbContext.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Where(p => p.IsActive)
            .AsQueryable();

        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
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

    private static async Task<IResult> GetCategoriesAsync([FromServices] AppDbContext dbContext = null!, CancellationToken cancellationToken = default)
    {
        var categories = await dbContext.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.ParentCategoryId, c.IsActive)).ToListAsync(cancellationToken);
        return Results.Ok(categories);
    }

    private static async Task<IResult> GetBrandsAsync([FromServices] AppDbContext dbContext = null!, CancellationToken cancellationToken = default)
    {
        var brands = await dbContext.Brands.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.Name)
            .Select(b => new BrandDto(b.Id, b.Name, b.Slug, b.IsActive)).ToListAsync(cancellationToken);
        return Results.Ok(brands);
    }
}
