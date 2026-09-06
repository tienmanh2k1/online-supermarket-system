# AI Intelligence Release Validation — Plan đơn giản

**Goal:** Chứng minh 5 bảng active, hai model ML.NET và UI flows hoạt động; không biến validation thành một dự án hạ tầng riêng.

**Scope:** 22 bảng active. `stock_alerts` và `background_job_runs` deferred; không kiểm thử schedule, lease, multi-instance lock hoặc 100-branch performance.

### VAL-01: Schema và backend gate

**Files:** migration/schema assertions và focused integration tests.

1. Apply migration trên MySQL test database sạch.
2. Assert đúng 22 bảng và constraints cốt lõi: review order item, inventory operation key, recommendation/forecast batch keys.
3. Chạy domain, infrastructure và API test projects; ghi rõ mọi WIP test ngoài scope nếu còn làm build hỏng.

**Verify:** `dotnet test backend/OnlineSupermarket.sln`

### VAL-02: ML và frontend gate

**Files:** model fixtures, frontend tests và build configuration.

1. Seed đủ views, completed orders và daily sales cho demo.
2. Chạy focused Matrix Factorization/SSA tests; xác nhận outputs finite, forecast không âm và fallback rõ nguồn.
3. Chạy Vitest/RTL và production build.

**Verify:** `npm --prefix frontend test -- --run && npm --prefix frontend run build`

### VAL-03: Ba E2E demo flows và docs

**Files:** Playwright smoke specs và tài liệu demo/API.

1. Completed order → create/update review.
2. Anonymous view → login merge → Admin refresh → personalized recommendation.
3. Completed sales → Admin refresh → forecast 7/14; cập nhật README/ERD/DFD/API docs theo scope 22 bảng.

**Verify:** chạy ba Playwright smoke specs trên dev stack.

## Release checklist

- Không có active route trả `jobRunId` hoặc UI polling job status.
- Không có active migration/task cho `stock_alerts` hay `background_job_runs`.
- Admin refresh trả 200 và UI hiển thị last generated time/metric.
- Evidence gồm test output, ba screenshots demo và commit hash.

**Done:** Có thể trình bày rõ input → ML.NET model → materialized output → customer/admin UI.
