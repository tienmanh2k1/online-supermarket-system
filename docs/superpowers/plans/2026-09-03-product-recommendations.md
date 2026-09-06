# Product Views và Recommendation ML — Implementation Plan đơn giản

**Goal:** Thu thập tương tác sản phẩm và tạo gợi ý cá nhân bằng ML.NET Matrix Factorization, có Global fallback.

**Architecture:** View capture/merge là synchronous API. Admin refresh đồng bộ tổng hợp views + completed purchases, train/predict, ghi result batch và trả 200.

**Tech Stack:** .NET 8, EF Core/MySQL, ML.NET Recommender, React, Vitest/RTL.

**Dependency:** VIEW-01..03 có thể làm song song với Inventory. REC-01 cần view/order DTO ổn định. Không phụ thuộc `background_job_runs`.

### VIEW-01: Event schema

**Files:** `ProductViewEvent` entity/config, `AppDbContext`, migration và tests.

1. Tạo event với product, nullable user/session và viewed time; không lưu IP/user-agent.
2. Map indexes cho user/session/product/time và generate migration.
3. Test metadata và yêu cầu có ít nhất user hoặc anonymous session.

**Verify:** `dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests/OnlineSupermarket.Infrastructure.Tests.csproj --filter FullyQualifiedName~ProductViewEvent`

### VIEW-02: Capture và session merge API

**Files:** product view/session endpoints, contracts và API tests.

1. Capture authenticated user từ JWT hoặc anonymous GUID hợp lệ.
2. Merge chỉ update anonymous rows chưa có owner; giữ tất cả events.
3. Test invalid GUID, missing product, auth merge và idempotent merge.

**Verify:** `dotnet test backend/tests/OnlineSupermarket.Api.Tests/OnlineSupermarket.Api.Tests.csproj --filter FullyQualifiedName~ProductView`

### VIEW-03: Frontend tracking

**Files:** localStorage session helper, product detail hook/API và tests.

1. Tạo/reuse anonymous GUID và gửi view một lần mỗi product/page session.
2. Sau login gọi merge best-effort, không chặn navigation.
3. Test authenticated/anonymous paths và không duplicate do re-render.

**Verify:** `npm --prefix frontend test -- --run`

### REC-01: Training data và Matrix Factorization

**Files:** recommendation input/output classes, `RecommendationModelService`, ML.NET package refs và model tests.

1. Tổng hợp view=1, completed purchase=5 theo user/product; bỏ anonymous chưa merge.
2. Train `MatrixFactorizationTrainer`, score unseen products và tính RMSE holdout khi đủ data.
3. Test train/predict finite score, exclude seen products và insufficient-data fallback.

**Verify:** `dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests/OnlineSupermarket.Infrastructure.Tests.csproj --filter FullyQualifiedName~RecommendationModel`

### REC-02: Results và refresh/read API

**Files:** `RecommendationResult` entity/config/migration, refresh/read services/endpoints và API tests.

1. Tạo result rows theo `batch_id`, audience, rank, algorithm, generated/expiry.
2. Refresh Admin train + persist atomically, trả 200 summary; không tạo job run.
3. Read API lọc availability và fallback Personal → Global, Similar → Global.

**Verify:** `dotnet test backend/tests/OnlineSupermarket.Api.Tests/OnlineSupermarket.Api.Tests.csproj --filter FullyQualifiedName~RecommendationEndpoints`

### REC-03: Customer UI và demo

**Files:** homepage/PDP recommendation sections, API client và tests.

1. Homepage hiển thị “Sản phẩm gợi ý cho bạn”; PDP hiển thị “Có thể bạn cũng thích”.
2. Cover personal/global source, loading, empty và error without breaking page.
3. Chạy tests/build và anonymous view → login/merge → refresh → personalized smoke flow.

**Verify:** `npm --prefix frontend test -- --run && npm --prefix frontend run build`

**Done:** Personalized path dùng model ML thật; Global top-selling chỉ là fallback được gắn `sourceScope=Global`.
