# Inventory Transactions và Demand Forecast ML — Implementation Plan đơn giản

**Goal:** Có inventory ledger tin cậy và forecast 7/14 ngày bằng ML.NET SSA, chạy thủ công từ Admin.

**Architecture:** Mọi inventory mutation đi qua một service và ghi ledger cùng transaction. Forecast refresh đồng bộ đọc completed sales/daily series, train SSA, ghi batch rồi trả 200.

**Tech Stack:** .NET 8, EF Core/MySQL, ML.NET TimeSeries, React, Vitest/RTL.

**Dependency:** INV-01..03 có thể làm song song với Reviews/Views. FCST-01 cần daily-sales contract từ Inventory/Orders; FCST-02..03 tiếp tục sau FCST-01. `stock_alerts` và `background_job_runs` đều deferred.

### INV-01: Ledger domain và migration

**Files:** inventory entity/configuration, `AppDbContext`, migration `AddInventoryTransactions`, domain/persistence tests.

1. Tạo immutable transaction entity với type, deltas, after snapshots, reference và operation key.
2. Map FK/index/unique nullable operation key; thêm DbSet và migration.
3. Test metadata, delta validation và append-only convention.

**Verify:** `dotnet test backend/tests/OnlineSupermarket.Domain.Tests/OnlineSupermarket.Domain.Tests.csproj --filter FullyQualifiedName~InventoryTransaction`

### INV-02: Atomic mutation service

**Files:** `InventoryMutationService`, checkout/order/admin callers và integration tests.

1. Viết focused tests cho Reserve, Release, Sale, ManualAdjustment và replay operation key.
2. Ghi BranchInventory + ledger trong cùng explicit transaction; batch lock inventories theo Id tăng dần.
3. Chuyển các caller hiện tại sang service chung; test rollback khi ledger insert lỗi.

**Verify:** `dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests/OnlineSupermarket.Infrastructure.Tests.csproj --filter FullyQualifiedName~InventoryMutation`

### INV-03: Admin history UI

**Files:** Admin inventory transaction endpoint/contracts, Inventory page/panel và tests.

1. Tạo paginated/filter API owner Admin-only.
2. Hiển thị type, delta, resulting quantities, reference và timestamp.
3. Test auth, filters, empty/error UI và frontend build.

**Verify:** `dotnet test backend/tests/OnlineSupermarket.Api.Tests/OnlineSupermarket.Api.Tests.csproj --filter FullyQualifiedName~InventoryTransaction && npm --prefix frontend run build`

### FCST-01: Daily series và SSA model

**Files:** forecast input/output classes, `DemandForecastModelService`, ML.NET package references và model tests.

1. Group completed sales theo branch/product/ngày; điền ngày zero-sale; chốt tối thiểu dữ liệu và holdout.
2. Train `ForecastBySsa`, dự đoán 7/14 ngày, clamp giá trị âm về 0 và tính MAE khi đủ data.
3. Test tiny deterministic series, invalid horizon và `InsufficientData`.

**Verify:** `dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests/OnlineSupermarket.Infrastructure.Tests.csproj --filter FullyQualifiedName~DemandForecastModel`

### FCST-02: Materialized batch và refresh API

**Files:** `DemandForecast` entity/config/migration, refresh/query endpoints và API tests.

1. Tạo `demand_forecasts` với `batch_id`, horizons 7/14, predicted quantity, metric/model/generated time.
2. `POST /api/admin/ai/forecast/refresh?branchId=` train + persist atomically và trả 200 summary.
3. GET đọc batch mới nhất; validate Admin và strict horizon 7|14.

**Verify:** `dotnet test backend/tests/OnlineSupermarket.Api.Tests/OnlineSupermarket.Api.Tests.csproj --filter FullyQualifiedName~ForecastEndpoints`

### FCST-03: Forecast UI và demo

**Files:** Admin AI/Forecast UI client/components và Vitest/RTL tests.

1. Tạo branch selector, 7/14 switch, table, last refresh, MAE/insufficient-data label.
2. Nút Refresh disable trong request, hiển thị success/error và reload result.
3. Chạy UI tests/build và completed sales → refresh → forecast smoke flow.

**Verify:** `npm --prefix frontend test -- --run && npm --prefix frontend run build`

**Done:** Ledger atomic; SSA model train/predict được; refresh trả 200; không có schedule/job status/stock alert.
