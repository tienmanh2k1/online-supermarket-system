using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Entities;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Infrastructure.Identity;
using OnlineSupermarket.Infrastructure.Persistence;
using OnlineSupermarket.Infrastructure.Persistence.SeedData;

namespace OnlineSupermarket.Infrastructure.Tests;

public sealed class DataSeederTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task SeedAllAsync_SeedsAllEntitiesCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasher();

        await DataSeeder.SeedAllAsync(context, hasher);

        // 1. Branches
        var branches = await context.Branches.ToListAsync();
        Assert.Equal(3, branches.Count);
        Assert.Contains(branches, b => b.Name == "AptechMart Quận 1");
        Assert.Contains(branches, b => b.Name == "AptechMart Quận 3");
        Assert.Contains(branches, b => b.Name == "AptechMart Bình Thạnh");

        // 2. Categories
        var categories = await context.Categories.ToListAsync();
        Assert.Equal(23, categories.Count);

        var bySlug = categories.ToDictionary(c => c.Slug);
        Assert.Equal(bySlug["tv-man-hinh"].Id, bySlug["tivi"].ParentCategoryId);
        Assert.Equal(bySlug["tv-man-hinh"].Id, bySlug["man-hinh-may-tinh"].ParentCategoryId);
        Assert.Equal(bySlug["am-thanh-loa"].Id, bySlug["tai-nghe"].ParentCategoryId);
        Assert.Equal(bySlug["am-thanh-loa"].Id, bySlug["loa"].ParentCategoryId);
        Assert.Null(bySlug["uncategorized"].ParentCategoryId);

        // 3. Brands
        var brands = await context.Brands.ToListAsync();
        Assert.Equal(10, brands.Count);
        Assert.Contains(brands, b => b.Slug == "samsung");
        Assert.Contains(brands, b => b.Slug == "apple");
        Assert.Contains(brands, b => b.Slug == "sony");
        Assert.Contains(brands, b => b.Slug == "lg");
        Assert.Contains(brands, b => b.Slug == "dell");
        Assert.Contains(brands, b => b.Slug == "asus");
        Assert.Contains(brands, b => b.Slug == "xiaomi");
        Assert.Contains(brands, b => b.Slug == "panasonic");
        Assert.Contains(brands, b => b.Slug == "jbl");
        Assert.Contains(brands, b => b.Slug == "canon");

        // 4. Products
        var products = await context.Products.ToListAsync();
        Assert.True(products.Count >= 20);
        Assert.Contains(products, p => p.Sku == "DT-SAM-001");
        Assert.Contains(products, p => p.Sku == "DT-APP-001");
        Assert.Contains(products, p => p.Sku == "LT-DEL-001");
        Assert.Contains(products, p => p.Sku == "LT-APP-001");
        Assert.Contains(products, p => p.Sku == "TV-SAM-001");
        Assert.Contains(products, p => p.Sku == "GD-PAN-001");
        Assert.Contains(products, p => p.Sku == "AT-JBL-001");

        // Every seeded product must live in a leaf category matching its SKU mapping.
        var parentIds = categories
            .Where(parent => categories.Any(child => child.ParentCategoryId == parent.Id))
            .Select(parent => parent.Id)
            .ToHashSet();
        Assert.DoesNotContain(products, product => parentIds.Contains(product.CategoryId));
        foreach (var product in products)
        {
            var expectedSlug = CatalogSeedTaxonomy.ResolveProductCategorySlug(product.Sku);
            Assert.Equal(bySlug[expectedSlug].Id, product.CategoryId);
        }

        // 5. Branch Inventories
        var inventories = await context.BranchInventories.ToListAsync();
        Assert.Equal(branches.Count * products.Count, inventories.Count);

        // 6. Users
        var users = await context.Users.ToListAsync();
        Assert.Equal(4, users.Count);
        var user1 = users.Single(u => u.Email == "user1@test.com");
        Assert.Equal("Nguyen Van An", user1.FullName);
        Assert.Equal(UserRole.Customer, user1.Role);
        Assert.True(hasher.VerifyPassword(user1.PasswordHash, "Test@123"));

        var admin = users.Single(u => u.Email == "admin@test.com");
        Assert.Equal("Admin User", admin.FullName);
        Assert.Equal(UserRole.Admin, admin.Role);

        // 7. Addresses
        var addresses = await context.Addresses.ToListAsync();
        Assert.Equal(4, addresses.Count);
        Assert.Equal(2, addresses.Count(a => a.UserId == user1.Id));

        // 8. Carts
        var carts = await context.Carts.Include(c => c.Items).ToListAsync();
        Assert.Single(carts);
        var cart = carts.First();
        Assert.Equal(3, cart.Items.Count);

        // 9. Orders
        var orders = await context.Orders.Include(o => o.Items).ToListAsync();
        Assert.True(orders.Count >= 4);
        Assert.Contains(orders, o => o.Status == OrderStatus.Completed);
        Assert.Contains(orders, o => o.Status == OrderStatus.Shipped);
        Assert.Contains(orders, o => o.Status == OrderStatus.Preparing);
    }

    [Fact]
    public async Task SeedAllAsync_IsIdempotent()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasher();

        await DataSeeder.SeedAllAsync(context, hasher);
        var branchCountFirst = await context.Branches.CountAsync();
        var productCountFirst = await context.Products.CountAsync();
        var userCountFirst = await context.Users.CountAsync();

        // Run seed again
        await DataSeeder.SeedAllAsync(context, hasher);

        Assert.Equal(branchCountFirst, await context.Branches.CountAsync());
        Assert.Equal(productCountFirst, await context.Products.CountAsync());
        Assert.Equal(userCountFirst, await context.Users.CountAsync());
    }

    [Fact]
    public void CatalogSeedTaxonomy_DefinesRootsLeavesAndUncategorized()
    {
        Assert.Equal(23, CatalogSeedTaxonomy.Categories.Count);
        var roots = CatalogSeedTaxonomy.Categories.Where(c => c.ParentSlug is null).ToList();
        var navigationParents = roots.Where(root =>
            CatalogSeedTaxonomy.Categories.Any(child => child.ParentSlug == root.Slug));

        Assert.Equal(9, roots.Count);
        Assert.Equal(8, navigationParents.Count());
        Assert.Equal(14, CatalogSeedTaxonomy.Categories.Count(c => c.ParentSlug is not null));
        Assert.Contains(CatalogSeedTaxonomy.Categories,
            c => c.Slug == "man-hinh-may-tinh" && c.ParentSlug == "tv-man-hinh");
        Assert.Contains(CatalogSeedTaxonomy.Categories,
            c => c.Slug == "uncategorized" && c.ParentSlug is null);
    }

    [Theory]
    [InlineData("TV-SAM-001", "tivi")]
    [InlineData("MH-SAM-001", "man-hinh-may-tinh")]
    [InlineData("AT-SON-001", "tai-nghe")]
    [InlineData("AT-JBL-002", "loa")]
    [InlineData("UNKNOWN-SKU", "uncategorized")]
    [InlineData(null, "uncategorized")]
    public void ResolveProductCategorySlug_ReturnsExpectedLeafOrFallback(
        string? sku,
        string expectedSlug)
    {
        Assert.Equal(expectedSlug, CatalogSeedTaxonomy.ResolveProductCategorySlug(sku));
    }

    [Fact]
    public void ResolveProductCategoryId_WithUnmappedSku_ReturnsUncategorizedId()
    {
        var uncategorizedId = Guid.NewGuid();
        var categoryIds = new Dictionary<string, Guid>
        {
            ["dien-thoai"] = Guid.NewGuid(),
            ["uncategorized"] = uncategorizedId,
        };

        var result = CatalogSeedTaxonomy.ResolveProductCategoryId("UNKNOWN-SKU", categoryIds);

        Assert.Equal(uncategorizedId, result);
    }

    [Fact]
    public async Task UnmappedSku_IsPersistedInUncategorized()
    {
        using var context = CreateInMemoryDbContext();
        await DataSeeder.SeedCategoriesAsync(context);
        await DataSeeder.SeedBrandsAsync(context);

        var categoryIds = await context.Categories.ToDictionaryAsync(c => c.Slug, c => c.Id);
        var appleBrandId = await context.Brands
            .Where(brand => brand.Slug == "apple")
            .Select(brand => brand.Id)
            .SingleAsync();
        var resolvedCategoryId = CatalogSeedTaxonomy.ResolveProductCategoryId(
            "UNMAPPED-SKU",
            categoryIds);
        var product = new Product(
            resolvedCategoryId,
            appleBrandId,
            "UNMAPPED-SKU",
            "Sản phẩm chờ phân loại",
            "san-pham-cho-phan-loai",
            null,
            100_000m,
            "cái",
            null);

        context.Products.Add(product);
        await context.SaveChangesAsync();

        Assert.Equal(categoryIds["uncategorized"], product.CategoryId);
        Assert.Equal(product.Id, (await context.Products.SingleAsync()).Id);
    }

    [Fact]
    public async Task Reconcile_MovesSeededProductsBackToLeafWithoutChangingIds()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasher();

        await DataSeeder.SeedAllAsync(context, hasher);
        var originalIds = (await context.Products.ToListAsync()).ToDictionary(p => p.Sku, p => p.Id);

        var categoriesBySlug = await context.Categories.ToDictionaryAsync(c => c.Slug, c => c.Id);
        var products = await context.Products
            .Where(p =>
                p.Sku == "TV-SAM-001" || p.Sku == "MH-SAM-001" ||
                p.Sku == "AT-SON-001" || p.Sku == "AT-JBL-002")
            .ToListAsync();

        // Simulate legacy rows still pointing at navigation parents.
        products.Single(p => p.Sku == "TV-SAM-001").ChangeCategory(categoriesBySlug["tv-man-hinh"]);
        products.Single(p => p.Sku == "MH-SAM-001").ChangeCategory(categoriesBySlug["tv-man-hinh"]);
        products.Single(p => p.Sku == "AT-SON-001").ChangeCategory(categoriesBySlug["am-thanh-loa"]);
        products.Single(p => p.Sku == "AT-JBL-002").ChangeCategory(categoriesBySlug["am-thanh-loa"]);
        await context.SaveChangesAsync();

        await DataSeeder.SeedAllAsync(context, hasher);

        var bySlug = await context.Categories.ToDictionaryAsync(c => c.Slug, c => c.Id);
        var productsAfter = (await context.Products.ToListAsync()).ToDictionary(p => p.Sku);

Assert.Equal(originalIds["TV-SAM-001"], productsAfter["TV-SAM-001"].Id);
        Assert.Equal(bySlug["tivi"], productsAfter["TV-SAM-001"].CategoryId);
        Assert.Equal(bySlug["man-hinh-may-tinh"], productsAfter["MH-SAM-001"].CategoryId);
        Assert.Equal(bySlug["tai-nghe"], productsAfter["AT-SON-001"].CategoryId);
        Assert.Equal(bySlug["loa"], productsAfter["AT-JBL-002"].CategoryId);
    }

    [Fact]
    public async Task Reconcile_MatchesSkuCaseInsensitively_WithRelationalSqlite()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var hasher = new PasswordHasher();
        await DataSeeder.SeedAllAsync(context, hasher);

        var tv = await context.Products.SingleAsync(p => p.Sku == "TV-SAM-001");
        context.Entry(tv).Property(p => p.Sku).CurrentValue = "tv-sam-001";
        tv.ChangeCategory((await context.Categories.SingleAsync(c => c.Slug == "tv-man-hinh")).Id);
        await context.SaveChangesAsync();

        await DataSeeder.SeedAllAsync(context, hasher);

        var bySlug = await context.Categories.ToDictionaryAsync(c => c.Slug, c => c.Id);
        var reconciled = await context.Products.SingleAsync(p => p.Sku == "tv-sam-001");
        Assert.Equal(bySlug["tivi"], reconciled.CategoryId);
    }

    [Fact]
    public async Task Reconcile_MatchesSkuCaseInsensitively()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasher();

        await DataSeeder.SeedAllAsync(context, hasher);
        var tv = await context.Products.SingleAsync(p => p.Sku == "TV-SAM-001");
        context.Entry(tv).Property(p => p.Sku).CurrentValue = "tv-sam-001";
        tv.ChangeCategory((await context.Categories.SingleAsync(c => c.Slug == "tv-man-hinh")).Id);
        await context.SaveChangesAsync();

        await DataSeeder.SeedAllAsync(context, hasher);

        var bySlug = await context.Categories.ToDictionaryAsync(c => c.Slug, c => c.Id);
        var reconciled = await context.Products.SingleAsync(p => p.Sku == "tv-sam-001");
        Assert.Equal(bySlug["tivi"], reconciled.CategoryId);
    }

    [Fact]
    public async Task SeedAllAsync_CorrectsCorruptedSeedData_WhenRunOnExistingCorruptedRecords()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasher();

        // 1. Initial seed
        await DataSeeder.SeedAllAsync(context, hasher);

        // 2. Corrupt some entities to simulate legacy mojibake (e.g., "AptechMart B??nh Th???nh", "c??i", corrupted category)
        var btBranch = await context.Branches.SingleAsync(b => b.Phone == "028 3891 9012");
        btBranch.Update("AptechMart B??nh Th???nh", "789 Nguy???n X??, B??nh Th???nh, TP.HCM", "028 3891 9012");

        var cat = await context.Categories.SingleAsync(c => c.Slug == "may-giat");
        cat.Update("M??y gi???t", "may-giat", cat.ParentCategoryId);

        var airpods = await context.Products.SingleAsync(p => p.Sku == "PK-APP-001");
        airpods.Update(
            airpods.CategoryId,
            airpods.BrandId,
            airpods.Sku,
            "AirPods Pro 2",
            airpods.Slug,
            System.Text.Encoding.ASCII.GetString(System.Text.Encoding.UTF8.GetBytes(airpods.Description!)),
            airpods.BasePrice,
            "c??i",
            airpods.ImageUrl);

        var user3 = await context.Users.SingleAsync(u => u.Email == "user3@test.com");
        var addr3 = await context.Addresses.SingleAsync(a => a.UserId == user3.Id && a.IsDefault);
        addr3.Update("Le Hoang Cuong", "0934567890", "456 ?i?n Bi?n Ph?", "Ph??ng 25", "B??nh Th???nh", "TP.HCM", "700000");

        await context.SaveChangesAsync();

        // 3. Re-run SeedAllAsync
        await DataSeeder.SeedAllAsync(context, hasher);

        // 4. Verify all corrupted records have been repaired
        var restoredBranch = await context.Branches.SingleAsync(b => b.Phone == "028 3891 9012");
        Assert.Equal("AptechMart Bình Thạnh", restoredBranch.Name);
        Assert.Equal("789 Nguyễn Xí, Bình Thạnh, TP.HCM", restoredBranch.Address);

        var restoredCat = await context.Categories.SingleAsync(c => c.Slug == "may-giat");
        Assert.Equal("Máy giặt", restoredCat.Name);

        var restoredProduct = await context.Products.SingleAsync(p => p.Sku == "PK-APP-001");
        Assert.Equal("cái", restoredProduct.Unit);
        Assert.DoesNotContain("?", restoredProduct.Description ?? string.Empty);

        var restoredAddr = await context.Addresses.SingleAsync(a => a.UserId == user3.Id && a.IsDefault);
        Assert.Equal("Bình Thạnh", restoredAddr.District);
        Assert.Equal("Phường 25", restoredAddr.Ward);
        Assert.Equal("456 Điện Biên Phủ", restoredAddr.Street);
    }

    [Theory]
    [InlineData("Có hỗ trợ lắp đặt không??")]
    [InlineData("M?? t?? b??? h???ng d???u")]
    public async Task SeedProductsAsync_PreservesCustomQuestionMarks_WhenRepairingUnit(string description)
    {
        using var context = CreateInMemoryDbContext();
        await DataSeeder.SeedAllAsync(context, new PasswordHasher());
        var product = await context.Products.SingleAsync(p => p.Sku == "DT-APP-001");
        var id = product.Id;
        product.Update(product.CategoryId, product.BrandId, product.Sku, "Ưu đãi đặc biệt??",
            product.Slug, description, product.BasePrice, "c??i", product.ImageUrl);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await DataSeeder.SeedProductsAsync(context);
        context.ChangeTracker.Clear();

        var restored = await context.Products.SingleAsync(p => p.Id == id);
        Assert.Equal("Ưu đãi đặc biệt??", restored.Name);
        Assert.Equal(description, restored.Description);
        Assert.Equal("cái", restored.Unit);
    }

    [Fact]
    public async Task SeedAddressesAsync_RepairsOnlyCorruptedFields_AndPreservesSimilarStreets()
    {
        using var context = CreateInMemoryDbContext();
        await DataSeeder.SeedAllAsync(context, new PasswordHasher());
        var user = await context.Users.SingleAsync(u => u.Email == "user1@test.com");
        var address = await context.Addresses.SingleAsync(a => a.UserId == user.Id && a.IsDefault);
        var id = address.Id;
        address.Update("Người nhận mới", "0901234000", "45 L?? Lai", "Phường mới",
            "Quận mới", "Thành phố mới", "123456");
        var extra = Address.Create(user.Id, "Khách", "0901234000",
            "45 Lê Lợi", "Phường?", "Quận khác", "TP.HCM", "123456");
        context.Addresses.Add(extra);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await DataSeeder.SeedAddressesAsync(context);
        await DataSeeder.SeedAddressesAsync(context);
        context.ChangeTracker.Clear();

        var restored = await context.Addresses.SingleAsync(a => a.Id == id);
        Assert.Equal("45 Lê Lai", restored.Street);
        Assert.Equal("Phường mới", restored.Ward);
        Assert.Equal("Quận mới", restored.District);
        Assert.Equal("Thành phố mới", restored.City);
        Assert.Equal("Người nhận mới", restored.RecipientName);
        Assert.Equal("0901234000", restored.Phone);
        Assert.Equal("123456", restored.PostalCode);
        var unchanged = await context.Addresses.SingleAsync(a => a.Id == extra.Id);
        Assert.Equal("45 Lê Lợi", unchanged.Street);
        Assert.Equal("Phường?", unchanged.Ward);
        Assert.Equal("Quận khác", unchanged.District);
    }

    [Fact]
    public async Task SeedAllAsync_PreservesAdminProductEdits_WhenSeederRunsAgain()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasher();

        // 1. Initial seed
        await DataSeeder.SeedAllAsync(context, hasher);

        // 2. Admin legitimately updates product Name, Description, and Unit (no mojibake)
        var product = await context.Products.SingleAsync(p => p.Sku == "DT-APP-001");
        product.Update(
            product.CategoryId,
            product.BrandId,
            product.Sku,
            "iPhone 15 Pro Max 256GB Titan Tự Nhiên",
            product.Slug,
            "Hàng chính hãng VN/A bảo hành 24 tháng.",
            product.BasePrice,
            "chiếc",
            product.ImageUrl);
        await context.SaveChangesAsync();

        // 3. Re-run SeedAllAsync
        await DataSeeder.SeedAllAsync(context, hasher);

        // 4. Verify admin edits are completely preserved
        var updated = await context.Products.SingleAsync(p => p.Sku == "DT-APP-001");
        Assert.Equal("iPhone 15 Pro Max 256GB Titan Tự Nhiên", updated.Name);
        Assert.Equal("Hàng chính hãng VN/A bảo hành 24 tháng.", updated.Description);
        Assert.Equal("chiếc", updated.Unit);
    }

    [Fact]
    public async Task SeedAllAsync_PreservesAdminCategoryEdits_WhenSeederRunsAgain()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasher();

        // 1. Initial seed
        await DataSeeder.SeedAllAsync(context, hasher);

        // 2. Admin legitimately renames a category (no mojibake)
        var category = await context.Categories.SingleAsync(c => c.Slug == "thiet-bi-gia-dung");
        category.Update("Thiết bị gia đình & Đời sống", category.Slug, category.ParentCategoryId);
        await context.SaveChangesAsync();

        // 3. Re-run SeedAllAsync
        await DataSeeder.SeedAllAsync(context, hasher);

        // 4. Verify category rename is preserved
        var updated = await context.Categories.SingleAsync(c => c.Slug == "thiet-bi-gia-dung");
        Assert.Equal("Thiết bị gia đình & Đời sống", updated.Name);
    }

    [Fact]
    public async Task SeedAllAsync_PreservesNonSeedBranchesAndAdminEdits_WhenSeederRunsAgain()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasher();

        // 1. Initial seed
        await DataSeeder.SeedAllAsync(context, hasher);

        // 2. Add a non-seed branch that happens to have "456" in its address
        var nonSeedBranch = new Branch("Chi nhánh Đà Nẵng", "456 Lê Duẩn, Quận Hải Châu, Đà Nẵng", "0236 3888 999", 16.0678m, 108.2208m);
        await context.Branches.AddAsync(nonSeedBranch);

        // 3. Admin updates seed branch 1 address (valid Vietnamese without ?)
        var branch1 = await context.Branches.SingleAsync(b => b.Phone == "028 3822 1234");
        branch1.Update("AptechMart Huệ Center", "123 Nguyễn Huệ, P. Bến Nghé, Quận 1, TP.HCM", branch1.Phone);
        await context.SaveChangesAsync();

        // 4. Re-run SeedAllAsync
        await DataSeeder.SeedAllAsync(context, hasher);

        // 5. Verify non-seed branch is untouched and admin edit is preserved
        var daNang = await context.Branches.SingleAsync(b => b.Phone == "0236 3888 999");
        Assert.Equal("Chi nhánh Đà Nẵng", daNang.Name);
        Assert.Equal("456 Lê Duẩn, Quận Hải Châu, Đà Nẵng", daNang.Address);

        var updatedBranch1 = await context.Branches.SingleAsync(b => b.Phone == "028 3822 1234");
        Assert.Equal("AptechMart Huệ Center", updatedBranch1.Name);
        Assert.Equal("123 Nguyễn Huệ, P. Bến Nghé, Quận 1, TP.HCM", updatedBranch1.Address);
    }

    [Fact]
    public async Task SeedAllAsync_PreservesUserCustomAddresses_WhenSeederRunsAgain()
    {
        using var context = CreateInMemoryDbContext();
        var hasher = new PasswordHasher();

        // 1. Initial seed
        await DataSeeder.SeedAllAsync(context, hasher);

        var user1 = await context.Users.SingleAsync(u => u.Email == "user1@test.com");
        var user2 = await context.Users.SingleAsync(u => u.Email == "user2@test.com");
        var user3 = await context.Users.SingleAsync(u => u.Email == "user3@test.com");

        // 2. Add extra addresses
        var user1Extra = Address.Create(user1.Id, "Nguyen Van An", "0912345678", "99 Hoàng Hoa Thám", "Phường 6", "Bình Thạnh", "TP.HCM", "700000", isDefault: false);
        var user2Extra = Address.Create(user2.Id, "Tran Thi Binh", "0923456789", "88 Hàm Nghi", "Bến Nghé", "Quận 1", "TP.HCM", "700000", isDefault: false);
        var user3Extra = Address.Create(user3.Id, "Le Hoang Cuong", "0934567890", "456 Lê Duẩn", "Thạch Thang", "Hải Châu", "Đà Nẵng", "550000", isDefault: false);

        await context.Addresses.AddRangeAsync(user1Extra, user2Extra, user3Extra);
        await context.SaveChangesAsync();

        // 3. Re-run SeedAllAsync
        await DataSeeder.SeedAllAsync(context, hasher);

        // 4. Verify all extra addresses remain intact
        var a1 = await context.Addresses.SingleAsync(a => a.Street == "99 Hoàng Hoa Thám");
        Assert.Equal("Phường 6", a1.Ward);
        Assert.Equal("Bình Thạnh", a1.District);

        var a2 = await context.Addresses.SingleAsync(a => a.Street == "88 Hàm Nghi");
        Assert.Equal("Bến Nghé", a2.Ward);
        Assert.Equal("Quận 1", a2.District);

        var a3 = await context.Addresses.SingleAsync(a => a.Street == "456 Lê Duẩn");
        Assert.Equal("Thạch Thang", a3.Ward);
        Assert.Equal("Hải Châu", a3.District);
    }
}
