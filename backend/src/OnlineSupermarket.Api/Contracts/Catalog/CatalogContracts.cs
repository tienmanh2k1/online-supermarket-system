namespace OnlineSupermarket.Api.Contracts.Catalog;

public record ProductSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string Sku,
    decimal BasePrice,
    string? ImageUrl,
    string CategoryName,
    string BrandName
);

public record BranchInventoryDto(
    Guid BranchId,
    decimal SellingPrice,
    int AvailableQuantity,
    int OnHand
);

public record ProductDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string Sku,
    string? Description,
    decimal BasePrice,
    string Unit,
    string? ImageUrl,
    Guid CategoryId,
    string CategoryName,
    Guid BrandId,
    string BrandName,
    BranchInventoryDto? BranchInventory
);

public record CategoryDto(
    Guid Id,
    string Name,
    string Slug,
    Guid? ParentCategoryId,
    bool IsActive
);

public record BrandDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive
);

public record PaginationMeta(
    long TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record PaginatedResponse<T>(
    IReadOnlyList<T> Data,
    PaginationMeta Meta
);

public record CreateProductRequest(
    string Name,
    string Sku,
    decimal BasePrice,
    string Unit,
    Guid CategoryId,
    Guid BrandId,
    string? Description,
    string? ImageUrl
);

public record UpdateProductRequest(
    string Name,
    string Sku,
    decimal BasePrice,
    string Unit,
    Guid CategoryId,
    Guid BrandId,
    string? Description,
    string? ImageUrl,
    bool IsActive
);

public record RecordProductViewRequest(
    Guid ProductId,
    string? UserId,
    string? SessionId
);
