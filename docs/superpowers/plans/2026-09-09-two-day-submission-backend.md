# Two-Day Submission Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cung cấp hai API chỉ đọc, có quyền Admin và contract ổn định cho Admin Dashboard và Sales Report; đồng thời khóa contract password reset hiện có để Frontend triển khai an toàn.

**Architecture:** Thêm `AdminReportingEndpoints` làm lớp endpoint mỏng, truy vấn trực tiếp `AppDbContext` bằng EF Core projection và trả DTO riêng trong `Contracts/Reporting`. Không thay đổi schema hoặc domain. Dashboard dùng một endpoint tổng hợp; sales report dùng khoảng ngày UTC bao gồm toàn bộ ngày `to` bằng điều kiện nửa mở `[from, to + 1 day)`.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core, MySQL, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-09-two-day-submission-pages-design.md`

## Global Constraints

- Deadline mục tiêu: 11/09/2026.
- Không migration và không thêm package.
- Chỉ `OrderStatus.Completed` đóng góp doanh thu.
- Dashboard `CompletedRevenue` là tổng toàn thời gian, không có date filter; UI phải ghi “Tổng doanh thu đơn hoàn tất”.
- `pendingOrders` gồm `Pending`, `Confirmed`, `Preparing`, `Ready`; không gồm `Shipped`, `Delivered`, `Completed`, `Cancelled`, `Failed`.
- `lowStockItems` đếm các dòng inventory có `QuantityOnHand <= ReorderLevel` và `ReorderLevel > 0`.
- Tất cả reporting endpoints yêu cầu policy `AdminOnly`.
- Sales report nhóm theo `Order.CreatedAtUtc` và khoảng ngày UTC, không theo thời điểm hoàn tất.
- Khoảng report tối đa 366 ngày tính inclusive; `DateOnly.MaxValue` không hợp lệ vì không thể tạo cận trên loại trừ.
- Lỗi validation report trả RFC 7807 `ProblemDetails`, không trả anonymous `{ message }`.

## Contract Produced For Frontend

```csharp
public sealed record DashboardSummaryDto(
    int TotalOrders,
    int PendingOrders,
    decimal CompletedRevenue,
    int LowStockItems,
    IReadOnlyList<RecentOrderDto> RecentOrders);

public sealed record RecentOrderDto(
    Guid Id,
    DateTime CreatedAtUtc,
    decimal TotalAmount,
    string Status,
    string FulfillmentType,
    int ItemCount);

public sealed record SalesReportDto(
    DateOnly From,
    DateOnly To,
    decimal TotalRevenue,
    int CompletedOrderCount,
    decimal AverageOrderValue,
    IReadOnlyList<DailySalesDto> Daily);

public sealed record DailySalesDto(DateOnly Date, decimal Revenue, int OrderCount);
```

---

### Task BE-1: Khép kín password reset cho cấu hình bản nộp

**Files:**
- Modify: `backend/tests/OnlineSupermarket.Api.Tests/Endpoints/PasswordResetEndpointsTests.cs`
- Modify: `backend/src/OnlineSupermarket.Api/Program.cs`
- Create: `backend/src/OnlineSupermarket.Api/Endpoints/DevEmailEndpoints.cs`
- Create: `backend/tests/OnlineSupermarket.Api.Tests/Endpoints/DevEmailEndpointsTests.cs`
- Verify: `backend/src/OnlineSupermarket.Infrastructure/DependencyInjection.cs`
- Verify: `backend/src/OnlineSupermarket.Api/appsettings.Development.json`
- Verify: `compose.yaml`

**Interfaces:**
- Verifies: `POST /api/auth/password-reset` và `POST /api/auth/password-reset/confirm`.
- Produces in Development only, with policy `AdminOnly`: `GET /api/dev/password-reset-emails/latest?email=<encoded-email> -> { email, resetUrl, capturedAtUtc }`. Caller phải gửi bearer token Admin.

- [ ] **Step 1: Thêm contract tests còn thiếu**

Test email rỗng trả 400; email tồn tại và không tồn tại trả cùng status/body; confirm thiếu token/password trả 400; token hết hạn và token dùng lại trả 400; token hợp lệ đổi được mật khẩu.

- [ ] **Step 2: Chạy test password reset**

Run: `dotnet test backend/tests/OnlineSupermarket.Api.Tests/OnlineSupermarket.Api.Tests.csproj --filter FullyQualifiedName~PasswordResetEndpointsTests`

Expected: PASS. Nếu fail do hành vi không đúng contract, sửa tối thiểu endpoint rồi chạy lại; không đổi response thành thông báo làm lộ email.

- [ ] **Step 3: Viết test fail cho dev mailbox và cấu hình khởi động**

Test Development map endpoint: anonymous trả 401, Customer trả 403; Admin truy vấn email không có trả 404, email có trả entry mới nhất. Test Production với `Email:UseDevMode=true` khởi động được nhưng mailbox vẫn trả 404 kể cả với token Admin; test riêng Production không có provider và không bật dev mode phải từ chối khởi động. Test cấu hình tương đương bản nộp (`ASPNETCORE_ENVIRONMENT=Development`, `Email:UseDevMode=true`) khởi động được.

- [ ] **Step 4: Map dev mailbox chỉ trong Development**

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapDevEmailEndpoints();
}
```

Trong `MapDevEmailEndpoints`, gắn `.RequireAuthorization("AdminOnly")` lên endpoint hoặc nhóm `/api/dev/password-reset-emails`. Handler đọc `DevEmailStore.Instance.GetAll()`, match email không phân biệt hoa thường, chọn `CapturedAtUtc` mới nhất và không log token/reset URL. Endpoint này không được map chỉ vì `Email:UseDevMode=true` trong Production.

- [ ] **Step 5: Chạy test và ghi kịch bản demo**

Run: `dotnet test backend/tests/OnlineSupermarket.Api.Tests/OnlineSupermarket.Api.Tests.csproj --filter "FullyQualifiedName~PasswordResetEndpointsTests|FullyQualifiedName~DevEmailEndpointsTests"`

Expected: PASS, bao gồm quyền mailbox và cấu hình môi trường.

Kịch bản bắt buộc ở README/hướng dẫn chạy: start `docker compose`; request reset từ UI cho Customer demo; đăng nhập Admin qua API và gửi bearer token Admin khi gọi dev mailbox để lấy `resetUrl`; ghép URL frontend nếu response là relative path; mở link; đổi mật khẩu; đăng nhập lại bằng Customer. Không ghi token hoặc reset URL vào log/báo cáo. Ghi rõ đây là mailbox demo, không phải email provider production.

- [ ] **Step 6: Commit**

Commit: `feat(api): expose development password reset mailbox`

### Task BE-2: DTO và authorization contract cho reporting

**Files:**
- Create: `backend/src/OnlineSupermarket.Api/Contracts/Reporting/AdminReportingDtos.cs`
- Create: `backend/src/OnlineSupermarket.Api/Endpoints/AdminReportingEndpoints.cs`
- Modify: `backend/src/OnlineSupermarket.Api/Program.cs`
- Create: `backend/tests/OnlineSupermarket.Api.Tests/Endpoints/AdminReportingEndpointsTests.cs`

**Interfaces:**
- Produces: `MapAdminReportingEndpoints(this IEndpointRouteBuilder routes)`.
- Produces: group `/api/admin` with `.RequireAuthorization("AdminOnly")`.

- [ ] **Step 1: Viết authorization tests fail**

```csharp
[Theory]
[InlineData("/api/admin/dashboard/summary")]
[InlineData("/api/admin/reports/sales?from=2026-09-01&to=2026-09-09")]
public async Task Reporting_WithoutToken_ReturnsUnauthorized(string path) { /* assert 401 */ }

[Theory]
[InlineData("/api/admin/dashboard/summary")]
[InlineData("/api/admin/reports/sales?from=2026-09-01&to=2026-09-09")]
public async Task Reporting_WithCustomerToken_ReturnsForbidden(string path) { /* assert 403 */ }
```

Tái sử dụng helper tạo client có token từ các admin endpoint tests.

- [ ] **Step 2: Chạy test đỏ**

Run: `dotnet test backend/tests/OnlineSupermarket.Api.Tests/OnlineSupermarket.Api.Tests.csproj --filter FullyQualifiedName~AdminReportingEndpointsTests`

Expected: FAIL/404 vì route chưa đăng ký.

- [ ] **Step 3: Tạo DTO, endpoint group và đăng ký trong Program**

```csharp
var group = routes.MapGroup("/api/admin")
    .WithTags("Admin-Reporting")
    .RequireAuthorization("AdminOnly");

group.MapGet("/dashboard/summary", GetDashboardSummaryAsync);
group.MapGet("/reports/sales", GetSalesReportAsync);
```

Thêm `app.MapAdminReportingEndpoints();` cạnh các admin endpoint registrations. Ban đầu handler có thể trả DTO rỗng hợp lệ để authorization tests xanh.

- [ ] **Step 4: Chạy test và commit**

Expected: anonymous 401, customer 403.

Commit: `feat(api): register admin reporting endpoints`

### Task BE-3: Dashboard summary query

**Files:**
- Modify: `backend/src/OnlineSupermarket.Api/Endpoints/AdminReportingEndpoints.cs`
- Modify: `backend/tests/OnlineSupermarket.Api.Tests/Endpoints/AdminReportingEndpointsTests.cs`

**Interfaces:**
- Produces: `GET /api/admin/dashboard/summary -> DashboardSummaryDto`.

- [ ] **Step 1: Viết aggregation test fail**

Seed orders ở các trạng thái `Pending`, `Preparing`, `Completed`, `Cancelled`; seed inventory dưới/bằng/trên reorder level. Assert totalOrders đếm tất cả, pendingOrders chỉ đếm bốn trạng thái quy định, revenue chỉ cộng Completed, lowStockItems đúng và recentOrders tối đa 5 theo `CreatedAtUtc` giảm dần.

- [ ] **Step 2: Chạy test đỏ**

Run: `dotnet test backend/tests/OnlineSupermarket.Api.Tests/OnlineSupermarket.Api.Tests.csproj --filter FullyQualifiedName~GetDashboardSummary`

Expected: FAIL vì handler chưa aggregate.

- [ ] **Step 3: Cài truy vấn tối thiểu bằng projection**

```csharp
var actionable = new[] {
    OrderStatus.Pending, OrderStatus.Confirmed,
    OrderStatus.Preparing, OrderStatus.Ready
};

var totalOrders = await db.Orders.CountAsync(ct);
var pendingOrders = await db.Orders.CountAsync(o => actionable.Contains(o.Status), ct);
var completedRevenue = await db.Orders
    .Where(o => o.Status == OrderStatus.Completed)
    .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
var lowStockItems = await db.BranchInventories
    .CountAsync(i => i.ReorderLevel > 0 && i.QuantityOnHand <= i.ReorderLevel, ct);
```

Recent orders dùng projection trực tiếp, `OrderByDescending`, `Take(5)` và `o.Items.Count` để tránh load entity graph.

- [ ] **Step 4: Thêm empty database test**

Assert trả 200, metrics bằng 0 và `recentOrders` là mảng rỗng, không phải `null`.

- [ ] **Step 5: Chạy test và commit**

Run: `dotnet test backend/tests/OnlineSupermarket.Api.Tests/OnlineSupermarket.Api.Tests.csproj --filter FullyQualifiedName~AdminReportingEndpointsTests`

Expected: PASS.

Commit: `feat(api): add admin dashboard summary`

### Task BE-4: Sales report validation và aggregation

**Files:**
- Modify: `backend/src/OnlineSupermarket.Api/Endpoints/AdminReportingEndpoints.cs`
- Modify: `backend/tests/OnlineSupermarket.Api.Tests/Endpoints/AdminReportingEndpointsTests.cs`

**Interfaces:**
- Produces: `GET /api/admin/reports/sales?from=YYYY-MM-DD&to=YYYY-MM-DD -> SalesReportDto`.

- [ ] **Step 1: Viết validation tests fail**

Test thiếu `from`/`to`, format sai, `from > to`, khoảng 367 ngày inclusive và `to=9999-12-31` đều trả 400 dạng RFC 7807. Test khoảng đúng 366 ngày inclusive được chấp nhận.

- [ ] **Step 2: Cài parse và validation**

```csharp
if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var fromDate) ||
    !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var toDate) ||
    fromDate > toDate ||
    toDate == DateOnly.MaxValue ||
    toDate.DayNumber - fromDate.DayNumber + 1 > 366)
    return Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid date range",
        detail: "Use UTC dates in yyyy-MM-dd format with at most 366 inclusive days.");
```

Thêm `using System.Globalization;`. Chuyển thành UTC `[fromDate at 00:00, toDate + 1 day at 00:00)`.

- [ ] **Step 3: Viết aggregation test fail**

Seed Completed trong/ngoài biên ngày tạo, cùng một ngày UTC có hai đơn, một đơn tạo trong kỳ nhưng hoàn tất sau kỳ, và Pending trong kỳ. Assert report lọc/nhóm theo `CreatedAtUtc`, chỉ Completed được cộng; daily group đúng; average bằng `totalRevenue / completedOrderCount`, hoặc 0 khi không có đơn.

- [ ] **Step 4: Cài query và fill ngày trống**

Project `CreatedAtUtc.Date`, `TotalAmount`; group trong database nếu provider test hỗ trợ, hoặc materialize projection nhỏ rồi group. Trả mọi ngày trong khoảng với revenue/orderCount bằng 0 để table frontend có trục ngày ổn định.

- [ ] **Step 5: Chạy test và commit**

Run: `dotnet test backend/tests/OnlineSupermarket.Api.Tests/OnlineSupermarket.Api.Tests.csproj --filter FullyQualifiedName~AdminReportingEndpointsTests`

Expected: PASS.

Commit: `feat(api): add minimal sales report`

### Task BE-5: OpenAPI, documentation và verification gate

**Files:**
- Modify: `docs/api/openapi.json`
- Modify: `docs/requirements/functional-requirements.md`
- Modify: `AptechMart_eProject_Submission/II_eProject_Report/sections/03_Analysis.md`
- Test: `backend/tests/OnlineSupermarket.Api.Tests/OpenApiContractTests.cs`

- [ ] **Step 1: Bổ sung OpenAPI metadata/tests**

Khai báo `.Produces<DashboardSummaryDto>()`, `.Produces<SalesReportDto>()`, `.ProducesProblem(400)` cho report. Test OpenAPI xác nhận hai paths, bearer security và response 400 có media type `application/problem+json`; endpoint test deserialize `ProblemDetails` và assert `Status`, `Title`, `Detail`.

- [ ] **Step 2: Chạy focused API tests**

Run: `dotnet test backend/tests/OnlineSupermarket.Api.Tests/OnlineSupermarket.Api.Tests.csproj --filter "FullyQualifiedName~AdminReportingEndpointsTests|FullyQualifiedName~PasswordResetEndpointsTests|FullyQualifiedName~DevEmailEndpointsTests|FullyQualifiedName~OpenApiContractTests"`

Expected: PASS.

- [ ] **Step 3: Build backend solution**

Run: `dotnet build OnlineSupermarket.slnx --no-restore`

Expected: build thành công, không thêm warning mới trong files reporting.

- [ ] **Step 4: Đồng bộ tài liệu**

Đổi FR-207 sang `✅ IMPLEMENTED` chỉ khi test và build xanh; ghi chính xác phạm vi tối thiểu (Completed orders nhóm theo ngày tạo UTC, daily totals, tối đa 366 ngày inclusive), không tuyên bố category/brand breakdown hoặc export. Backend không sửa `docs/architecture/sitemap.md`; Frontend sở hữu file đó ở gate tích hợp cuối.

- [ ] **Step 5: Commit gate**

Commit: `docs: document admin reporting contract`

## Backend Handoff Checklist

- Gửi cho Frontend ví dụ JSON thật của hai endpoint.
- Xác nhận enum/status dùng PascalCase như DTO order hiện tại.
- Xác nhận ngày report là `YYYY-MM-DD`, timezone tính theo UTC.
- Gửi endpoint dev mailbox và xác nhận nó chỉ tồn tại trong Development, yêu cầu bearer token Admin; Frontend dùng API request context Admin riêng để chạy E2E reset-password của Customer.
- Nếu integration MySQL chưa chạy, ghi rõ; không gọi là verified production behavior chỉ từ InMemory tests.
