using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Entities;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Promotions;
using OnlineSupermarket.Domain.Shopping;
using OnlineSupermarket.Infrastructure.Identity;
using OnlineSupermarket.Infrastructure.Persistence.SeedData;

namespace OnlineSupermarket.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAllAsync(AppDbContext context, IPasswordHasher hasher)
    {
        await SeedBranchesAsync(context);
        await SeedCategoriesAsync(context);
        await SeedBrandsAsync(context);
        await SeedProductsAsync(context);
        await ReconcileSeedProductCategoriesAsync(context);
        await SeedBranchInventoriesAsync(context);
        await SeedUsersAsync(context, hasher);
        await SeedAddressesAsync(context);
        await SeedCartsAsync(context);
        await SeedOrdersAsync(context);
        await SeedPromotionsAsync(context);
    }

    public static async Task SeedPromotionsAsync(AppDbContext context)
    {
        if (await context.Promotions.AnyAsync()) return;

        var promotions = new List<Promotion>
        {
            Promotion.Create("WELCOME10", DiscountType.Percentage, 10m),
            Promotion.Create("SAVE50K", DiscountType.FixedAmount, 50_000m, minOrderAmount: 500_000m, usageLimit: 100),
        };

        await context.Promotions.AddRangeAsync(promotions);
        await context.SaveChangesAsync();
    }

    public static async Task SeedBranchesAsync(AppDbContext context)
    {
        var branches = await context.Branches.ToListAsync();
        if (branches.Count == 0)
        {
            var newBranches = new List<Branch>
            {
                new(
                    "AptechMart Quận 1",
                    "123 Nguyễn Huệ, Quận 1, TP.HCM",
                    "028 3822 1234",
                    10.7769m,
                    106.7009m),
                new(
                    "AptechMart Quận 3",
                    "456 Đường 3 Tháng 2, Quận 10, TP.HCM",
                    "028 3862 5678",
                    10.7791m,
                    106.6801m),
                new(
                    "AptechMart Bình Thạnh",
                    "789 Nguyễn Xí, Bình Thạnh, TP.HCM",
                    "028 3891 9012",
                    10.8037m,
                    106.7195m),
            };

            await context.Branches.AddRangeAsync(newBranches);
            await context.SaveChangesAsync();
            return;
        }

        // Conditional correction for existing seed branches corrupted with mojibake
        bool branchesUpdated = false;
        var knownMojibakeBranch1Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AptechMart Qu??n 1", "AptechMart Qu?n 1", "AptechMart Qu???n 1"
        };
        var knownMojibakeBranch2Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AptechMart Qu??n 3", "AptechMart Qu?n 3", "AptechMart Qu???n 3"
        };
        var knownMojibakeBranch3Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AptechMart B??nh Th???nh", "AptechMart B?nh Th?nh", "AptechMart B??nh Th??nh"
        };

        foreach (var branch in branches)
        {
            if (branch.Phone == "028 3822 1234" || knownMojibakeBranch1Names.Contains(branch.Name))
            {
                string targetName = knownMojibakeBranch1Names.Contains(branch.Name) ? "AptechMart Quận 1" : branch.Name;
                string targetAddress = (branch.Address.StartsWith("123 Nguy", StringComparison.OrdinalIgnoreCase) && branch.Address.Contains('?'))
                    ? "123 Nguyễn Huệ, Quận 1, TP.HCM"
                    : branch.Address;

                if (targetName != branch.Name || targetAddress != branch.Address)
                {
                    branch.Update(targetName, targetAddress, branch.Phone);
                    branchesUpdated = true;
                }
            }
            else if (branch.Phone == "028 3862 5678" || knownMojibakeBranch2Names.Contains(branch.Name))
            {
                string targetName = knownMojibakeBranch2Names.Contains(branch.Name) ? "AptechMart Quận 3" : branch.Name;
                string targetAddress = (branch.Address.StartsWith("456 ", StringComparison.OrdinalIgnoreCase) && branch.Address.Contains('?'))
                    ? "456 Đường 3 Tháng 2, Quận 10, TP.HCM"
                    : branch.Address;

                if (targetName != branch.Name || targetAddress != branch.Address)
                {
                    branch.Update(targetName, targetAddress, branch.Phone);
                    branchesUpdated = true;
                }
            }
            else if (branch.Phone == "028 3891 9012" || knownMojibakeBranch3Names.Contains(branch.Name))
            {
                string targetName = knownMojibakeBranch3Names.Contains(branch.Name) ? "AptechMart Bình Thạnh" : branch.Name;
                string targetAddress = (branch.Address.StartsWith("789 Nguy", StringComparison.OrdinalIgnoreCase) && branch.Address.Contains('?'))
                    ? "789 Nguyễn Xí, Bình Thạnh, TP.HCM"
                    : branch.Address;

                if (targetName != branch.Name || targetAddress != branch.Address)
                {
                    branch.Update(targetName, targetAddress, branch.Phone);
                    branchesUpdated = true;
                }
            }
        }

        if (branchesUpdated)
        {
            await context.SaveChangesAsync();
        }
    }

    private static readonly Dictionary<string, HashSet<string>> KnownMojibakeCategoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dien-thoai-tablet"] = new() { "??i???n tho???i & Tablet", "??i?n tho?i & Tablet", "?i?n tho?i & Tablet", "??i??n tho??i & Tablet" },
        ["laptop-may-tinh"] = new() { "Laptop & M??y t??nh", "Laptop & M?y t?nh", "Laptop & M??y t?nh" },
        ["tv-man-hinh"] = new() { "TV & M??n h??nh", "TV & M?n h?nh", "TV & M??n h?nh" },
        ["thiet-bi-gia-dung"] = new() { "Thi???t b??? gia d???ng", "Thi?t b? gia d?ng", "Thi?t b? gia d??ng", "Thi??t b?? gia d??ng" },
        ["am-thanh-loa"] = new() { "??m thanh & Loa", "?m thanh & Loa" },
        ["phu-kien"] = new() { "Ph??? ki???n", "Ph? ki?n", "Ph?? ki??n" },
        ["uncategorized"] = new() { "Ch??a ph??n lo???i", "Ch?a ph?n lo?i", "Ch??a ph??n lo??i" },
        ["dien-thoai"] = new() { "??i???n tho???i", "??i?n tho?i", "?i?n tho?i", "??i??n tho??i" },
        ["may-tinh-bang"] = new() { "M??y t??nh b???ng", "M?y t?nh b?ng", "M??y t??nh b??ng" },
        ["man-hinh-may-tinh"] = new() { "M??n h??nh m??y t??nh", "M?n h?nh m?y t?nh", "M??n h?nh m??y t?nh" },
        ["may-lanh"] = new() { "M??y l???nh", "M?y l?nh", "M??y l??nh" },
        ["tu-lanh"] = new() { "T??? l???nh", "T? l?nh", "T?? l??nh" },
        ["may-giat"] = new() { "M??y gi???t", "M?y gi?t", "M??y gi??t" },
        ["chuot"] = new() { "Chu???t", "Chu?t", "Chu??t" },
        ["may-choi-game"] = new() { "M??y ch??i game", "M?y ch?i game", "M??y ch?i game" },
        ["may-anh"] = new() { "M??y ???nh", "M?y ?nh", "M??y ??nh" },
    };

    public static async Task SeedCategoriesAsync(AppDbContext context)
    {
        var categoriesBySlug = await context.Categories.ToDictionaryAsync(c => c.Slug);

        Category EnsureCategory(CategorySeedDefinition definition, Guid? parentId)
        {
            if (categoriesBySlug.TryGetValue(definition.Slug, out var existing))
            {
                if (KnownMojibakeCategoryNames.TryGetValue(definition.Slug, out var corruptedNames) &&
                    corruptedNames.Contains(existing.Name))
                {
                    existing.Update(definition.Name, existing.Slug, existing.ParentCategoryId ?? parentId);
                }
                return existing;
            }

            var category = new Category(definition.Name, definition.Slug, parentId);
            context.Categories.Add(category);
            categoriesBySlug.Add(definition.Slug, category);
            return category;
        }

        foreach (var root in CatalogSeedTaxonomy.Categories.Where(c => c.ParentSlug is null))
        {
            EnsureCategory(root, null);
        }
        await context.SaveChangesAsync();

        foreach (var child in CatalogSeedTaxonomy.Categories.Where(c => c.ParentSlug is not null))
        {
            EnsureCategory(child, categoriesBySlug[child.ParentSlug!].Id);
        }
        await context.SaveChangesAsync();
    }

    public static async Task SeedBrandsAsync(AppDbContext context)
    {
        if (await context.Brands.AnyAsync()) return;

        var brands = new List<Brand>
        {
            new("Samsung", "samsung"),
            new("Apple", "apple"),
            new("Sony", "sony"),
            new("LG", "lg"),
            new("Dell", "dell"),
            new("ASUS", "asus"),
            new("Xiaomi", "xiaomi"),
            new("Panasonic", "panasonic"),
            new("JBL", "jbl"),
            new("Canon", "canon"),
        };

        await context.Brands.AddRangeAsync(brands);
        await context.SaveChangesAsync();
    }

    public static async Task SeedProductsAsync(AppDbContext context)
    {
        var categories = await context.Categories.ToDictionaryAsync(c => c.Slug, c => c.Id);
        var brands = await context.Brands.ToDictionaryAsync(b => b.Slug, b => b.Id);

        Guid CategoryIdFor(string sku) => CatalogSeedTaxonomy.ResolveProductCategoryId(sku, categories);

        var products = new List<Product>
        {
            // Điện thoại & Tablet
            new(CategoryIdFor("DT-SAM-001"), brands["samsung"], "DT-SAM-001",
                "Samsung Galaxy S24 Ultra 256GB", "samsung-galaxy-s24-ultra-256gb",
                "Flagship Samsung với chip Snapdragon 8 Gen 3, màn hình Dynamic AMOLED 2X 6.8 inch và camera 200MP chống rung OIS.",
                28990000m, "cái", "https://images.unsplash.com/photo-1610945265064-0e34e5519bbf?w=500&q=80"),

            new(CategoryIdFor("DT-SAM-002"), brands["samsung"], "DT-SAM-002",
                "Samsung Galaxy Z Fold6", "samsung-galaxy-z-fold6",
                "Điện thoại gập cao cấp nhất của Samsung với thiết kế mỏng nhẹ, bản lề Flex Hinge bền bỉ và hỗ trợ Galaxy AI.",
                41990000m, "cái", "https://images.unsplash.com/photo-1580910051074-3eb694886505?w=500&q=80"),

            new(CategoryIdFor("DT-APP-001"), brands["apple"], "DT-APP-001",
                "iPhone 15 Pro Max 256GB", "iphone-15-pro-max-256gb",
                "Thiết kế khung viền titan siêu bền, chip Apple A17 Pro mạnh mẽ, camera zoom quang học 5x sắc nét và cổng sạc USB-C.",
                34990000m, "cái", "https://images.unsplash.com/photo-1695048133142-1a20484d2569?w=500&q=80"),

            new(CategoryIdFor("DT-APP-002"), brands["apple"], "DT-APP-002",
                "iPhone 15 128GB", "iphone-15-128gb",
                "Màn hình Super Retina XDR với Dynamic Island, camera chính 48MP và mặt lưng kính pha màu sang trọng.",
                22990000m, "cái", "https://images.unsplash.com/photo-1510557880182-3d4d3cba35a5?w=500&q=80"),

            new(CategoryIdFor("DT-XIA-001"), brands["xiaomi"], "DT-XIA-001",
                "Xiaomi Redmi Note 13 Pro", "xiaomi-redmi-note-13-pro",
                "Camera 200MP chống rung OIS, màn hình AMOLED 1.5K 120Hz mượt mà và sạc siêu nhanh 67W.",
                6990000m, "cái", "https://images.unsplash.com/photo-1598327105666-5b89351aff97?w=500&q=80"),

            new(CategoryIdFor("DT-SAM-003"), brands["samsung"], "DT-SAM-003",
                "Samsung Galaxy Tab S9 FE", "samsung-galaxy-tab-s9-fe",
                "Máy tính bảng chuẩn kháng nước kháng bụi IP68, màn hình 10.9 inch 90Hz đi kèm bút S Pen đa năng.",
                12990000m, "cái", "https://images.unsplash.com/photo-1544244015-0df4b3ffc6b0?w=500&q=80"),

            new(CategoryIdFor("DT-APP-003"), brands["apple"], "DT-APP-003",
                "iPad Mini 6 64GB WiFi", "ipad-mini-6-64gb-wifi",
                "Thiết kế viền mỏng toàn màn hình Liquid Retina 8.3 inch, chip A15 Bionic mạnh mẽ trong kiểu dáng nhỏ gọn.",
                13490000m, "cái", "https://images.unsplash.com/photo-1561154464-82e9adf32764?w=500&q=80"),

            // Laptop & Máy tính
            new(CategoryIdFor("LT-DEL-001"), brands["dell"], "LT-DEL-001",
                "Dell XPS 15 9530 (i7, 16GB, 512GB)", "dell-xps-15-9530",
                "Laptop doanh nhân cao cấp màn hình OLED tràn viền 3.5K, vi xử lý Intel Core i7 thế hệ 13 và card đồ họa rời RTX 4050.",
                35990000m, "cái", "https://images.unsplash.com/photo-1593642632823-8f785ba67e45?w=500&q=80"),

            new(CategoryIdFor("LT-APP-001"), brands["apple"], "LT-APP-001",
                "MacBook Pro 14\" M3 Pro", "macbook-pro-14-m3-pro",
                "Laptop chuyên nghiệp trang bị vi xử lý Apple M3 Pro, màn hình Liquid Retina XDR 120Hz và thời lượng pin 18 giờ.",
                45990000m, "cái", "https://images.unsplash.com/photo-1517336714731-489689fd1ca8?w=500&q=80"),

            new(CategoryIdFor("LT-APP-002"), brands["apple"], "LT-APP-002",
                "MacBook Air 15\" M3", "macbook-air-15-m3",
                "Thiết kế siêu mỏng nhẹ thanh lịch, chip Apple M3 hiệu năng mạnh mẽ cùng màn hình Liquid Retina 15.3 inch rực rỡ.",
                32990000m, "cái", "https://images.unsplash.com/photo-1611186871348-b1ce696e52c9?w=500&q=80"),

            new(CategoryIdFor("LT-ASUS-001"), brands["asus"], "LT-ASUS-001",
                "ASUS ROG Strix G16 Gaming", "asus-rog-strix-g16-gaming",
                "Cỗ máy gaming đỉnh cao với Intel Core i7 Gen 13, card đồ họa NVIDIA RTX 4060 và màn hình 165Hz chuẩn thi đấu eSports.",
                28990000m, "cái", "https://images.unsplash.com/photo-1603302576837-37561b2e2302?w=500&q=80"),

            new(CategoryIdFor("LT-ASUS-002"), brands["asus"], "LT-ASUS-002",
                "ASUS ZenBook 14 OLED", "asus-zenbook-14-oled",
                "Laptop mỏng nhẹ thời thượng với màn hình Lumina OLED 3K 120Hz, chip Intel Core Ultra AI và pin khủng 75Wh.",
                21990000m, "cái", "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?w=500&q=80"),

            // TV & Màn hình
            new(CategoryIdFor("TV-SAM-001"), brands["samsung"], "TV-SAM-001",
                "Samsung Neo QLED 4K 65 inch QA65QN90C", "samsung-neo-qled-4k-65-inch-qa65qn90c",
                "Smart TV 65 inch công nghệ Quantum Matrix mini LED đỉnh cao, chip xử lý Neural Quantum 4K AI và loa Dolby Atmos sống động.",
                45990000m, "cái", "https://images.unsplash.com/photo-1593359677879-a4bb92f829d1?w=500&q=80"),

            new(CategoryIdFor("TV-LG-001"), brands["lg"], "TV-LG-001",
                "LG OLED evo 4K 55 inch C4", "lg-oled-evo-4k-55-inch-c4",
                "Màn hình OLED điểm ảnh tự phát sáng, bộ xử lý α9 AI Gen7 4K, tần số quét 144Hz hỗ trợ chơi game đỉnh cao và Dolby Vision.",
                32990000m, "cái", "https://images.unsplash.com/photo-1593784991095-a205069470b6?w=500&q=80"),

            new(CategoryIdFor("TV-SON-001"), brands["sony"], "TV-SON-001",
                "Sony Bravia 4K 55 inch XR-55A80L", "sony-bravia-4k-55-inch-xr-55a80l",
                "Màn hình OLED sắc sảo tích hợp bộ xử lý nhận thức Cognitive Processor XR tái tạo độ sâu và màu sắc chân thực như mắt người.",
                28990000m, "cái", "https://images.unsplash.com/photo-1552975084-6e027cd345c2?w=500&q=80"),

            new(CategoryIdFor("MH-SAM-001"), brands["samsung"], "MH-SAM-001",
                "Samsung M8 Smart Monitor 32\"", "samsung-m8-smart-monitor-32",
                "Màn hình 32 inch 4K đa năng tích hợp hệ điều hành Smart Hub xem phim không cần PC, kèm webcam từ tính SlimFit độ nét cao.",
                14990000m, "cái", "https://images.unsplash.com/photo-1527443224154-c4a3942d3acf?w=500&q=80"),

            // Thiết bị gia dụng
            new(CategoryIdFor("GD-PAN-001"), brands["panasonic"], "GD-PAN-001",
                "Panasonic Inverter 1.5 HP CU/CS-U12XK", "panasonic-inverter-1-5-hp-cu-cs-u12xk",
                "Máy lạnh Inverter cao cấp tích hợp công nghệ lọc không khí nanoe™ X khử mùi ức chế vi khuẩn và chế độ làm lạnh nhanh iAUTO-X.",
                12990000m, "cái", "https://images.unsplash.com/photo-1621905251189-08b45d6a269e?w=500&q=80"),

            new(CategoryIdFor("GD-LG-001"), brands["lg"], "GD-LG-001",
                "LG Inverter 2 HP B19END", "lg-inverter-2-hp-b19end",
                "Điều hòa nhiệt độ LG Dual Inverter tiết kiệm điện đến 70%, làm lạnh nhanh hơn 40% với gas R32 thân thiện môi trường.",
                16990000m, "cái", "https://images.unsplash.com/photo-1585338107529-13afc5f02586?w=500&q=80"),

            new(CategoryIdFor("GD-LG-002"), brands["lg"], "GD-LG-002",
                "LG Door-in-Door 601L InstaView", "lg-door-in-door-601l-instaview",
                "Tủ lạnh Side by Side 601 lít sang trọng, cửa kính InstaView gõ 2 lần nhìn thấu bên trong hạn chế thất thoát khí lạnh.",
                42990000m, "cái", "https://images.unsplash.com/photo-1584269600464-37b1b58a9fe7?w=500&q=80"),

            new(CategoryIdFor("GD-PAN-002"), brands["panasonic"], "GD-PAN-002",
                "Panasonic Giant 8.5kg NA-F85V1", "panasonic-giant-8-5kg-na-f85v1",
                "Máy giặt lồng đứng 8.5kg với mâm giặt Active Wave tạo luồng nước đa chiều đánh bay vết bẩn cứng đầu nhanh chóng.",
                14990000m, "cái", "https://images.unsplash.com/photo-1626806787461-102c1bfaaea1?w=500&q=80"),

            // Âm thanh & Loa
            new(CategoryIdFor("AT-JBL-001"), brands["jbl"], "AT-JBL-001",
                "JBL PartyBox 310", "jbl-partybox-310",
                "Loa di động tiệc tùng công suất 240W uy lực, tích hợp hiệu ứng ánh sáng đèn LED đồng bộ điệu nhạc và pin khủng 18 giờ.",
                14990000m, "cái", "https://images.unsplash.com/photo-1545454675-3531b543be5d?w=500&q=80"),

            new(CategoryIdFor("AT-SON-001"), brands["sony"], "AT-SON-001",
                "Sony WH-1000XM5", "sony-wh-1000xm5",
                "Tai nghe chụp tai chống ồn chủ động không dây hàng đầu với 2 bộ xử lý âm thanh HD V1 và QN1, hỗ trợ âm thanh Hi-Res LDAC.",
                8990000m, "cái", "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?w=500&q=80"),

            new(CategoryIdFor("AT-JBL-002"), brands["jbl"], "AT-JBL-002",
                "JBL Flip 6", "jbl-flip-6",
                "Loa Bluetooth di động nhỏ gọn chống bụi nước chuẩn IP67, hệ thống loa 2 chiều âm thanh mạnh mẽ và thời lượng pin 12 giờ.",
                3490000m, "cái", "https://images.unsplash.com/photo-1608043152269-423dbba4e7e1?w=500&q=80"),

            new(CategoryIdFor("PK-APP-001"), brands["apple"], "PK-APP-001",
                "AirPods Pro 2", "airpods-pro-2",
                "Tai nghe không dây Apple thế hệ 2 với chip H2 chống ồn chủ động gấp 2 lần, âm thanh không gian cá nhân hóa và hộp sạc MagSafe.",
                5990000m, "cái", "https://images.unsplash.com/photo-1600294037681-c80b4cb5b434?w=500&q=80"),

            // Phụ kiện
            new(CategoryIdFor("PK-APP-002"), brands["apple"], "PK-APP-002",
                "Apple Magic Mouse", "apple-magic-mouse",
                "Chuột không dây Bluetooth thiết kế thanh mảnh bề mặt cảm ứng đa điểm Multi-Touch mượt mà tối ưu cho máy Mac.",
                2190000m, "cái", "https://images.unsplash.com/photo-1615663245857-ac93bb7c39e7?w=500&q=80"),

            // Game & Gaming
            new(CategoryIdFor("GM-SON-001"), brands["sony"], "GM-SON-001",
                "Sony PlayStation 5 Slim", "sony-playstation-5-slim",
                "Máy chơi game console PS5 phiên bản Slim ổ đĩa siêu tốc 1TB SSD, hỗ trợ ray tracing chân thực và đồ họa 4K 120Hz mượt mà.",
                14990000m, "cái", "https://images.unsplash.com/photo-1606813907291-d86efa9b94db?w=500&q=80"),

            // Camera & An ninh
            new(CategoryIdFor("CAM-CAN-001"), brands["canon"], "CAM-CAN-001",
                "Canon EOS R50 Mirrorless Kit 18-45mm", "canon-eos-r50-kit-18-45mm",
                "Máy ảnh mirrorless cảm biến APS-C 24.2MP nhỏ nhẹ lý tưởng cho người sáng tạo nội dung, hỗ trợ quay video 4K sắc nét và Dual Pixel AF.",
                18990000m, "cái", "https://images.unsplash.com/photo-1516035069371-29a1b244cc32?w=500&q=80"),

            new(CategoryIdFor("CAM-XIA-001"), brands["xiaomi"], "CAM-XIA-001",
                "Xiaomi Smart Camera C300 2K", "xiaomi-smart-camera-c300-2k",
                "Camera an ninh xoay 360 độ góc nhìn toàn cảnh, độ phân giải 2K siêu nét, đàm thoại 2 chiều và cảnh báo chuyển động bằng AI thông minh.",
                890000m, "cái", "https://images.unsplash.com/photo-1557597774-9d273605dfa9?w=500&q=80"),
        };

        var existingProducts = await context.Products.ToDictionaryAsync(p => p.Sku, StringComparer.OrdinalIgnoreCase);
        if (existingProducts.Count == 0)
        {
            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
            return;
        }

        bool productsUpdated = false;
        foreach (var seedProduct in products)
        {
            if (existingProducts.TryGetValue(seedProduct.Sku, out var existing))
            {
                string targetName = IsKnownLegacyMojibake(existing.Name, seedProduct.Name)
                    ? seedProduct.Name
                    : existing.Name;

                string? targetDescription = IsKnownLegacyMojibake(existing.Description, seedProduct.Description)
                    ? seedProduct.Description
                    : existing.Description;

                string targetUnit = IsKnownLegacyMojibake(existing.Unit, seedProduct.Unit)
                    ? seedProduct.Unit
                    : existing.Unit;

                if (targetName != existing.Name ||
                    targetDescription != existing.Description ||
                    targetUnit != existing.Unit)
                {
                    existing.Update(
                        existing.CategoryId,
                        existing.BrandId,
                        existing.Sku,
                        targetName,
                        existing.Slug,
                        targetDescription,
                        existing.BasePrice,
                        targetUnit,
                        existing.ImageUrl);
                    productsUpdated = true;
                }
            }
            else
            {
                await context.Products.AddAsync(seedProduct);
                productsUpdated = true;
            }
        }

        if (productsUpdated)
        {
            await context.SaveChangesAsync();
        }
    }

    private static bool IsKnownLegacyMojibake(string? value, string? seedValue)
    {
        if (value is null || seedValue is null || value == seedValue) return false;

        // Legacy ASCII conversion replaced either each UTF-8 byte or each Unicode character.
        // Compare the entire field against its own seed value, never punctuation alone.
        return value == System.Text.Encoding.ASCII.GetString(System.Text.Encoding.UTF8.GetBytes(seedValue)) ||
            value == System.Text.Encoding.ASCII.GetString(System.Text.Encoding.ASCII.GetBytes(seedValue));
    }

    private static async Task ReconcileSeedProductCategoriesAsync(AppDbContext context)
    {
        var categoriesBySlug = await context.Categories.ToDictionaryAsync(c => c.Slug, c => c.Id);
        var mappedSkus = CatalogSeedTaxonomy.ProductCategorySlugBySku.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var products = await context.Products
            .Where(p => mappedSkus.Contains(p.Sku.ToUpper()))
            .ToListAsync();

        foreach (var product in products)
        {
            var targetSlug = CatalogSeedTaxonomy.ResolveProductCategorySlug(product.Sku);
            var targetCategoryId = categoriesBySlug[targetSlug];
            if (product.CategoryId != targetCategoryId)
            {
                product.ChangeCategory(targetCategoryId);
            }
        }

        await context.SaveChangesAsync();
    }

    public static async Task SeedBranchInventoriesAsync(AppDbContext context)
    {
        if (await context.BranchInventories.AnyAsync()) return;

        var branches = await context.Branches.ToListAsync();
        var products = await context.Products.ToListAsync();

        var inventories = new List<BranchInventory>();
        foreach (var branch in branches)
        {
            int baseQuantity = branch.Name.Contains("Quận 1") ? 30 : branch.Name.Contains("Quận 3") ? 25 : 40;

            foreach (var product in products)
            {
                var inv = BranchInventory.Create(
                    branch.Id,
                    product.Id,
                    product.BasePrice,
                    quantityOnHand: baseQuantity,
                    reorderLevel: 5);

                inventories.Add(inv);
            }
        }

        await context.BranchInventories.AddRangeAsync(inventories);
        await context.SaveChangesAsync();
    }

    public static async Task SeedUsersAsync(AppDbContext context, IPasswordHasher hasher)
    {
        if (await context.Users.AnyAsync()) return;

        var hashedPassword = hasher.HashPassword("Test@123");

        var users = new List<User>
        {
            User.Create("user1@test.com", hashedPassword, "Nguyen Van An", "0912345678", UserRole.Customer),
            User.Create("user2@test.com", hashedPassword, "Tran Thi Binh", "0923456789", UserRole.Customer),
            User.Create("user3@test.com", hashedPassword, "Le Hoang Cuong", "0934567890", UserRole.Customer),
            User.Create("admin@test.com", hashedPassword, "Admin User", "0901234567", UserRole.Admin),
        };

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();
    }

    public static async Task SeedAddressesAsync(AppDbContext context)
    {
        var users = await context.Users.ToDictionaryAsync(u => u.Email, u => u.Id);
        if (!users.ContainsKey("user1@test.com") || !users.ContainsKey("user2@test.com") || !users.ContainsKey("user3@test.com"))
        {
            return;
        }

        var existingAddresses = await context.Addresses.ToListAsync();
        if (existingAddresses.Count == 0)
        {
            var addresses = new List<Address>
            {
                Address.Create(users["user1@test.com"], "Nguyen Van An", "0912345678", "45 Lê Lai", "Bến Thành", "Quận 1", "TP.HCM", "700000", isDefault: true),
                Address.Create(users["user1@test.com"], "Nguyen Van An", "0912345678", "78 Nguyễn Trãi", "Phường 2", "Quận 5", "TP.HCM", "700000", isDefault: false),
                Address.Create(users["user2@test.com"], "Tran Thi Binh", "0923456789", "123 Pasteur", "Bến Nghé", "Quận 1", "TP.HCM", "700000", isDefault: true),
                Address.Create(users["user3@test.com"], "Le Hoang Cuong", "0934567890", "456 Điện Biên Phủ", "Phường 25", "Bình Thạnh", "TP.HCM", "700000", isDefault: true),
            };

            await context.Addresses.AddRangeAsync(addresses);
            await context.SaveChangesAsync();
            return;
        }

        var seedAddresses = new[]
        {
            (UserId: users["user1@test.com"], Street: "45 Lê Lai", Ward: "Bến Thành", District: "Quận 1"),
            (UserId: users["user1@test.com"], Street: "78 Nguyễn Trãi", Ward: "Phường 2", District: "Quận 5"),
            (UserId: users["user2@test.com"], Street: "123 Pasteur", Ward: "Bến Nghé", District: "Quận 1"),
            (UserId: users["user3@test.com"], Street: "456 Điện Biên Phủ", Ward: "Phường 25", District: "Bình Thạnh"),
        };
        bool addressesUpdated = false;
        static string Repair(string current, string seed) => IsKnownLegacyMojibake(current, seed) ? seed : current;

        foreach (var seed in seedAddresses)
        {
            foreach (var addr in existingAddresses.Where(a => a.UserId == seed.UserId &&
                (a.Street == seed.Street || IsKnownLegacyMojibake(a.Street, seed.Street))))
            {
                var street = Repair(addr.Street, seed.Street);
                var ward = Repair(addr.Ward, seed.Ward);
                var district = Repair(addr.District, seed.District);
                var city = Repair(addr.City, "TP.HCM");
                if (street == addr.Street && ward == addr.Ward && district == addr.District && city == addr.City)
                    continue;

                addr.Update(addr.RecipientName, addr.Phone, street, ward, district, city, addr.PostalCode);
                addressesUpdated = true;
            }
        }

        if (addressesUpdated)
        {
            await context.SaveChangesAsync();
        }
    }

    public static async Task SeedCartsAsync(AppDbContext context)
    {
        if (await context.Carts.AnyAsync()) return;

        var user2 = await context.Users.FirstOrDefaultAsync(u => u.Email == "user2@test.com");
        var branch1 = await context.Branches.FirstOrDefaultAsync(b => b.Name.Contains("Quận 1"));

        if (user2 == null || branch1 == null) return;

        var iphone15ProMax = await context.Products.FirstOrDefaultAsync(p => p.Sku == "DT-APP-001");
        var airpods = await context.Products.FirstOrDefaultAsync(p => p.Sku == "PK-APP-001");
        var ipadMini = await context.Products.FirstOrDefaultAsync(p => p.Sku == "DT-APP-003");

        if (iphone15ProMax == null || airpods == null || ipadMini == null) return;

        var inventories = await context.BranchInventories
            .Where(bi => bi.BranchId == branch1.Id && (bi.ProductId == iphone15ProMax.Id || bi.ProductId == airpods.Id || bi.ProductId == ipadMini.Id))
            .ToDictionaryAsync(bi => bi.ProductId);

        var cart = new Cart(user2.Id, branch1.Id);
        if (inventories.TryGetValue(iphone15ProMax.Id, out var iphoneInv))
        {
            cart.AddItem(iphone15ProMax.Id, iphoneInv.Id, iphoneInv.SellingPrice, 1);
        }
        if (inventories.TryGetValue(airpods.Id, out var airpodsInv))
        {
            cart.AddItem(airpods.Id, airpodsInv.Id, airpodsInv.SellingPrice, 1);
        }
        if (inventories.TryGetValue(ipadMini.Id, out var ipadInv))
        {
            cart.AddItem(ipadMini.Id, ipadInv.Id, ipadInv.SellingPrice, 1);
        }

        await context.Carts.AddAsync(cart);
        await context.SaveChangesAsync();
    }

    public static async Task SeedOrdersAsync(AppDbContext context)
    {
        if (await context.Orders.AnyAsync()) return;

        var user1 = await context.Users.FirstOrDefaultAsync(u => u.Email == "user1@test.com");
        var user2 = await context.Users.FirstOrDefaultAsync(u => u.Email == "user2@test.com");
        var user3 = await context.Users.FirstOrDefaultAsync(u => u.Email == "user3@test.com");

        var branch1 = await context.Branches.FirstOrDefaultAsync(b => b.Name.Contains("Quận 1"));
        var branch3 = await context.Branches.FirstOrDefaultAsync(b => b.Name.Contains("Bình Thạnh"));

        if (user1 == null || user2 == null || user3 == null || branch1 == null || branch3 == null) return;

        var user1Address1 = await context.Addresses.FirstOrDefaultAsync(a => a.UserId == user1.Id && a.IsDefault);
        var user1Address2 = await context.Addresses.FirstOrDefaultAsync(a => a.UserId == user1.Id && !a.IsDefault);
        var user3Address = await context.Addresses.FirstOrDefaultAsync(a => a.UserId == user3.Id && a.IsDefault);

        var s24 = await context.Products.FirstOrDefaultAsync(p => p.Sku == "DT-SAM-001");
        var macbookAir = await context.Products.FirstOrDefaultAsync(p => p.Sku == "LT-APP-002");
        var dellXps = await context.Products.FirstOrDefaultAsync(p => p.Sku == "LT-DEL-001");
        var iphone15 = await context.Products.FirstOrDefaultAsync(p => p.Sku == "DT-APP-002");
        var jblFlip = await context.Products.FirstOrDefaultAsync(p => p.Sku == "AT-JBL-002");
        var lgFridge = await context.Products.FirstOrDefaultAsync(p => p.Sku == "GD-LG-002");

        var orders = new List<Order>();

        // Order 1: user1, Completed, Delivery
        if (s24 != null && macbookAir != null)
        {
            var items1 = new List<(Guid ProductId, string ProductName, string Sku, decimal UnitPrice, int Quantity, decimal LineTotal)>
            {
                (s24.Id, s24.Name, s24.Sku, s24.BasePrice, 1, s24.BasePrice),
                (macbookAir.Id, macbookAir.Name, macbookAir.Sku, macbookAir.BasePrice, 1, macbookAir.BasePrice)
            };
            var subtotal1 = s24.BasePrice + macbookAir.BasePrice;
            var order1 = Order.Create(
                user1.Id,
                branch1.Id,
                "Delivery",
                user1.FullName,
                user1.Phone ?? "0912345678",
                "45 Lê Lai, Bến Thành, Quận 1, TP.HCM",
                user1Address1?.Id,
                items1,
                subtotal1,
                0m,
                0m,
                subtotal1);
            order1.SetStatus(OrderStatus.Completed, "Đơn hàng đã giao thành công");
            orders.Add(order1);
        }

        // Order 2: user1, Shipped, Delivery
        if (dellXps != null)
        {
            var items2 = new List<(Guid ProductId, string ProductName, string Sku, decimal UnitPrice, int Quantity, decimal LineTotal)>
            {
                (dellXps.Id, dellXps.Name, dellXps.Sku, dellXps.BasePrice, 1, dellXps.BasePrice)
            };
            var subtotal2 = dellXps.BasePrice;
            var order2 = Order.Create(
                user1.Id,
                branch1.Id,
                "Delivery",
                user1.FullName,
                user1.Phone ?? "0912345678",
                "78 Nguyễn Trãi, Phường 2, Quận 5, TP.HCM",
                user1Address2?.Id,
                items2,
                subtotal2,
                0m,
                0m,
                subtotal2);
            order2.SetStatus(OrderStatus.Shipped, "Đang giao hàng");
            orders.Add(order2);
        }

        // Order 3: user2, Preparing, Pickup
        if (iphone15 != null && jblFlip != null)
        {
            var items3 = new List<(Guid ProductId, string ProductName, string Sku, decimal UnitPrice, int Quantity, decimal LineTotal)>
            {
                (iphone15.Id, iphone15.Name, iphone15.Sku, iphone15.BasePrice, 1, iphone15.BasePrice),
                (jblFlip.Id, jblFlip.Name, jblFlip.Sku, jblFlip.BasePrice, 1, jblFlip.BasePrice)
            };
            var subtotal3 = iphone15.BasePrice + jblFlip.BasePrice;
            var order3 = Order.Create(
                user2.Id,
                branch1.Id,
                "Pickup",
                user2.FullName,
                user2.Phone ?? "0923456789",
                "Nhận tại chi nhánh: AptechMart Quận 1 (123 Nguyễn Huệ, Quận 1, TP.HCM)",
                null,
                items3,
                subtotal3,
                0m,
                0m,
                subtotal3);
            order3.SetStatus(OrderStatus.Preparing, "Đang chuẩn bị hàng tại chi nhánh");
            orders.Add(order3);
        }

        // Order 4: user3, Completed, Delivery
        if (lgFridge != null)
        {
            var items4 = new List<(Guid ProductId, string ProductName, string Sku, decimal UnitPrice, int Quantity, decimal LineTotal)>
            {
                (lgFridge.Id, lgFridge.Name, lgFridge.Sku, lgFridge.BasePrice, 1, lgFridge.BasePrice)
            };
            var subtotal4 = lgFridge.BasePrice;
            var order4 = Order.Create(
                user3.Id,
                branch3.Id,
                "Delivery",
                user3.FullName,
                user3.Phone ?? "0934567890",
                "456 Điện Biên Phủ, Phường 25, Bình Thạnh, TP.HCM",
                user3Address?.Id,
                items4,
                subtotal4,
                0m,
                0m,
                subtotal4);
            order4.SetStatus(OrderStatus.Completed, "Đơn hàng đã giao thành công");
            orders.Add(order4);
        }

        if (orders.Count > 0)
        {
            await context.Orders.AddRangeAsync(orders);
            await context.SaveChangesAsync();
        }
    }
}
