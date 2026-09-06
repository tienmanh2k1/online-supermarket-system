# Verified Reviews — Implementation Plan đơn giản

**Goal:** Khách chỉ review đúng OrderItem đã mua trong order Completed; hiển thị/viết review tại Product detail và Order detail.

**Architecture:** API đồng bộ, unique `order_item_id` là data-integrity gate; backend luôn lấy user từ JWT và product từ OrderItem.

**Tech Stack:** .NET 8, EF Core/MySQL, React, Vitest/RTL.

**Independence:** Board này làm độc lập với inventory và AI. Nếu test WIP làm solution build hỏng, chạy project/filter của Reviews trước; sửa hoặc tạm exclude file WIP trong chính branch Reviews, không bắt board khác chịu dependency.

### REV-01: Domain và migration

**Files:**
- Create/modify: `backend/src/OnlineSupermarket.Domain/Reviews/Review.cs`
- Create/modify: `backend/src/OnlineSupermarket.Infrastructure/Persistence/Configurations/ReviewConfiguration.cs`
- Modify: `backend/src/OnlineSupermarket.Infrastructure/Persistence/AppDbContext.cs`
- Create: migration `AddReviews`
- Test: `backend/tests/OnlineSupermarket.Domain.Tests/Reviews/ReviewTests.cs`

**Steps:**

1. Viết tests rating 1–5, comment max 2.000, create/update timestamps.
2. Tạo entity và EF mapping với FK Restrict, unique `order_item_id`, index product/date.
3. Generate/inspect migration; chạy focused tests.

**Verify:** `dotnet test backend/tests/OnlineSupermarket.Domain.Tests/OnlineSupermarket.Domain.Tests.csproj --filter FullyQualifiedName~ReviewTests`

### REV-02: Verified review API

**Files:**
- Modify: `backend/src/OnlineSupermarket.Api/Endpoints/ReviewEndpoints.cs`
- Modify: `backend/src/OnlineSupermarket.Api/Contracts/Reviews/ReviewContracts.cs`
- Modify: order detail contract/endpoint hiện có
- Test: `backend/tests/OnlineSupermarket.Api.Tests/Reviews/ReviewEndpointsTests.cs`

**Steps:**

1. Tạo POST/PUT/list/eligibility contracts; validate auth, ownership và Completed status.
2. Trả 409 khi `order_item_id` đã có review; order detail trả `canReview`/`reviewId`.
3. Test 401/403/404/409, pagination và update owner-only.

**Verify:** `dotnet test backend/tests/OnlineSupermarket.Api.Tests/OnlineSupermarket.Api.Tests.csproj --filter FullyQualifiedName~ReviewEndpointsTests`

### REV-03: Customer UI và regression

**Files:**
- Modify: Product detail component/API
- Modify: Order detail component/API
- Test: colocated Vitest/RTL tests

**Steps:**

1. Product detail hiển thị aggregate/list/form; Order detail hiển thị CTA viết hoặc sửa.
2. Cover loading, empty, validation, eligibility và API error states.
3. Chạy frontend tests/build và completed-order → review smoke flow.

**Verify:** `npm --prefix frontend test -- --run && npm --prefix frontend run build`

**Done:** Ba task pass; không yêu cầu bất kỳ AI/background-job board nào.
