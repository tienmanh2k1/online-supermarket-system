# Two-Day Submission Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hoàn thiện giao diện khôi phục mật khẩu, 404, Admin Dashboard, Sales Report và hai trang pháp lý trong phạm vi nộp bài hai ngày.

**Architecture:** Các trang mới đi theo feature folders hiện có và được nối route tại `App.tsx`. Frontend chỉ phụ thuộc hai endpoint báo cáo đọc dữ liệu do task Backend cung cấp; password reset dùng endpoint hiện có. Mỗi trang tự quản lý loading/error/empty state và tiếp tục dùng `AuthContext`, `AdminRoute`, `httpClient` hiện tại.

**Tech Stack:** React 19, TypeScript 5.9, React Router 7, Vitest 4, Testing Library, CSS hiện có.

**Spec:** `docs/superpowers/specs/2026-09-09-two-day-submission-pages-design.md`

## Global Constraints

- Deadline mục tiêu: 11/09/2026; hoàn thành scope bắt buộc trước khi làm `/support`.
- Không thêm dependency frontend.
- Không làm Customer Dashboard, wishlist, widget tùy biến hoặc export báo cáo.
- Không thay đổi contract Backend bên dưới nếu chưa đồng bộ task Backend.
- Mọi request có loading, error, success/empty phù hợp; không hiển thị số 0 giả khi request lỗi.

## Backend Contract Consumed

```ts
export interface DashboardSummaryDto {
  totalOrders: number
  pendingOrders: number
  completedRevenue: number
  lowStockItems: number
  recentOrders: Array<{
    id: string
    createdAtUtc: string
    totalAmount: number
    status: string
    fulfillmentType: string
    itemCount: number
  }>
}

export interface SalesReportDto {
  from: string
  to: string
  totalRevenue: number
  completedOrderCount: number
  averageOrderValue: number
  daily: Array<{ date: string; revenue: number; orderCount: number }>
}
```

- `GET /api/admin/dashboard/summary`
- `GET /api/admin/reports/sales?from=YYYY-MM-DD&to=YYYY-MM-DD`

---

### Task FE-1: Password-reset API và giao diện yêu cầu email

**Files:**
- Modify: `frontend/src/api/authApi.ts`
- Modify: `frontend/src/features/auth/AuthModal.tsx`
- Modify: `frontend/src/features/auth/LoginForm.tsx`
- Create: `frontend/src/features/auth/ForgotPasswordForm.tsx`
- Test: `frontend/src/features/auth/Auth.test.tsx`

**Interfaces:**
- Produces: `requestPasswordResetApi(email: string, signal?: AbortSignal): Promise<{ message: string }>`.
- Produces: `ForgotPasswordForm({ onBackToLogin }: { onBackToLogin: () => void })`.

- [ ] **Step 1: Viết test fail cho nút “Quên mật khẩu?” và submit email**

Mock `requestPasswordResetApi`, mở modal login, click “Quên mật khẩu?”, nhập `user@example.com`, submit, rồi assert thông báo trung tính xuất hiện và không chứa thông tin email tồn tại hay không.

- [ ] **Step 2: Chạy test đỏ**

Run: `npm test -- --run src/features/auth/Auth.test.tsx`

Expected: FAIL vì chưa có nút/form/API.

- [ ] **Step 3: Thêm API và state `forgot` cho modal**

```ts
export function requestPasswordResetApi(email: string, signal?: AbortSignal) {
  return postJson<{ message: string }>('/auth/password-reset', { email }, { signal })
}
```

Đổi `mode` thành `'login' | 'register' | 'forgot'`; `LoginForm` nhận `onForgotPassword`; `ForgotPasswordForm` validate email rỗng, khóa nút khi gửi và luôn dùng copy thành công: “Nếu email tồn tại, chúng tôi đã gửi liên kết đặt lại mật khẩu.”

- [ ] **Step 4: Chạy test xanh và commit**

Run: `npm test -- --run src/features/auth/Auth.test.tsx`

Expected: PASS.

Commit: `feat(frontend): add forgot password request flow`

### Task FE-2: Trang xác nhận đặt lại mật khẩu

**Files:**
- Modify: `frontend/src/api/authApi.ts`
- Create: `frontend/src/features/auth/ResetPasswordPage.tsx`
- Create: `frontend/src/features/auth/ResetPasswordPage.css`
- Create: `frontend/src/features/auth/ResetPasswordPage.test.tsx`
- Modify: `frontend/src/App.tsx`

**Interfaces:**
- Produces: `confirmPasswordResetApi(token: string, newPassword: string, signal?: AbortSignal): Promise<{ message: string }>`.
- Route: `/reset-password?token=<opaque-token>`.

- [ ] **Step 1: Viết test fail cho token thiếu, password không khớp và submit thành công**

Test riêng ba case: thiếu token không gọi API; hai password khác nhau báo lỗi client; token hợp lệ gọi API `{ token, newPassword }` và hiển thị nút quay lại đăng nhập/trang chủ.

- [ ] **Step 2: Chạy test đỏ**

Run: `npm test -- --run src/features/auth/ResetPasswordPage.test.tsx`

Expected: FAIL vì component chưa tồn tại.

- [ ] **Step 3: Cài API, page và route**

```ts
export function confirmPasswordResetApi(token: string, newPassword: string, signal?: AbortSignal) {
  return postJson<{ message: string }>('/auth/password-reset/confirm', { token, newPassword }, { signal })
}
```

Page đọc token bằng `useSearchParams`, có `new-password` và `confirm-password`, `autoComplete="new-password"`, validate rỗng/không khớp, xử lý lỗi 400 thành “Liên kết không hợp lệ hoặc đã hết hạn.”

- [ ] **Step 4: Chạy test và commit**

Run: `npm test -- --run src/features/auth/ResetPasswordPage.test.tsx src/App.test.tsx`

Expected: PASS.

Commit: `feat(frontend): add reset password page`

### Task FE-3: Trang 404 và route fallback

**Files:**
- Create: `frontend/src/features/system/NotFoundPage.tsx`
- Create: `frontend/src/features/system/NotFoundPage.css`
- Create: `frontend/src/features/system/NotFoundPage.test.tsx`
- Modify: `frontend/src/App.tsx`

**Interfaces:**
- Route: `*` render trong `AppShell`.

- [ ] **Step 1: Viết route test fail**

Render app tại `/duong-dan-khong-ton-tai`, assert heading “Không tìm thấy trang”, link `/` và `/products`.

- [ ] **Step 2: Chạy test đỏ**

Run: `npm test -- --run src/features/system/NotFoundPage.test.tsx`

Expected: FAIL vì route hiện render shell trống.

- [ ] **Step 3: Tạo page và đặt `<Route path="*" element={<NotFoundPage />} />` cuối nhóm route public**

Không redirect tự động; dùng `<Link>` để giữ SPA navigation.

- [ ] **Step 4: Chạy test và commit**

Run: `npm test -- --run src/features/system/NotFoundPage.test.tsx src/App.test.tsx`

Expected: PASS.

Commit: `feat(frontend): add not found page`

### Task FE-4: API báo cáo và Admin Dashboard

**Files:**
- Modify: `frontend/src/api/adminApi.ts`
- Create: `frontend/src/features/admin/AdminDashboardPage.tsx`
- Create: `frontend/src/features/admin/AdminDashboardPage.css`
- Create: `frontend/src/features/admin/AdminDashboardPage.test.tsx`
- Modify: `frontend/src/features/admin/AdminLayout.tsx`
- Modify: `frontend/src/App.tsx`

**Interfaces:**
- Consumes: `DashboardSummaryDto` và `GET /api/admin/dashboard/summary`.
- Produces: `/admin/dashboard`; `/admin` redirect tới `dashboard`.

- [ ] **Step 1: Viết test fail cho ready, empty, error/retry và navigation**

Mock `adminApi.getDashboardSummary`. Assert bốn metric, recent order links, spinner `aria-busy`, error `role="alert"`, retry gọi API lần hai; assert sidebar có “Tổng quan”.

- [ ] **Step 2: Chạy test đỏ**

Run: `npm test -- --run src/features/admin/AdminDashboardPage.test.tsx`

Expected: FAIL vì API/page chưa tồn tại.

- [ ] **Step 3: Thêm DTO/API và page**

```ts
getDashboardSummary: (token: string, signal?: AbortSignal) =>
  getJson<DashboardSummaryDto>('/admin/dashboard/summary', { token, signal })
```

Page lấy `accessToken`, abort request khi unmount, format tiền bằng `Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })`; recent orders link tới `/admin/orders/:id`.

- [ ] **Step 4: Nối route và sidebar**

Thêm `{ to: '/admin/dashboard', label: 'Tổng quan', icon: '📊' }` đầu `NAV_ITEMS`; đổi admin index redirect từ `catalog/categories` sang `dashboard`.

- [ ] **Step 5: Chạy test và commit**

Run: `npm test -- --run src/features/admin/AdminDashboardPage.test.tsx src/features/admin/AdminRoute.test.tsx src/App.test.tsx`

Expected: PASS.

Commit: `feat(frontend): add admin dashboard`

### Task FE-5: Sales Report tối thiểu

**Files:**
- Modify: `frontend/src/api/adminApi.ts`
- Create: `frontend/src/features/admin/AdminSalesReportPage.tsx`
- Create: `frontend/src/features/admin/AdminSalesReportPage.css`
- Create: `frontend/src/features/admin/AdminSalesReportPage.test.tsx`
- Modify: `frontend/src/features/admin/AdminLayout.tsx`
- Modify: `frontend/src/App.tsx`

**Interfaces:**
- Consumes: `SalesReportDto` và `GET /api/admin/reports/sales`.
- Produces: `/admin/reports/sales`.

- [ ] **Step 1: Viết test fail cho mặc định 30 ngày, preset, validation và state**

Đóng băng thời gian trong Vitest; assert lần đầu gửi ngày kết thúc hiện tại và ngày bắt đầu 29 ngày trước. Test `from > to` không gọi API, preset 7 ngày tải lại, empty daily table hiển thị zero-state, lỗi có retry.

- [ ] **Step 2: Chạy test đỏ**

Run: `npm test -- --run src/features/admin/AdminSalesReportPage.test.tsx`

Expected: FAIL vì page/API chưa tồn tại.

- [ ] **Step 3: Thêm query builder và page**

```ts
getSalesReport: (from: string, to: string, token: string, signal?: AbortSignal) => {
  const query = new URLSearchParams({ from, to })
  return getJson<SalesReportDto>(`/admin/reports/sales?${query}`, { token, signal })
}
```

Dùng `<input type="date">`, ba preset 7 ngày/30 ngày/tháng này, ba KPI, average order value và table `date/revenue/orderCount`; không thêm chart library.

- [ ] **Step 4: Nối route/sidebar, chạy test và commit**

Run: `npm test -- --run src/features/admin/AdminSalesReportPage.test.tsx src/App.test.tsx`

Expected: PASS.

Commit: `feat(frontend): add minimal sales report`

### Task FE-6: Privacy, Terms và footer links

**Files:**
- Create: `frontend/src/features/legal/PrivacyPage.tsx`
- Create: `frontend/src/features/legal/TermsPage.tsx`
- Create: `frontend/src/features/legal/LegalPages.css`
- Create: `frontend/src/features/legal/LegalPages.test.tsx`
- Modify: `frontend/src/app/AppShell.tsx`
- Modify: `frontend/src/App.tsx`

**Interfaces:**
- Produces: `/privacy`, `/terms` và footer links toàn site.

- [ ] **Step 1: Viết test fail cho routes và footer links**

Assert Privacy mô tả tài khoản, địa chỉ, đơn hàng và thanh toán; Terms mô tả tài khoản, đặt hàng và giới hạn sandbox; footer link dùng route nội bộ.

- [ ] **Step 2: Chạy test đỏ**

Run: `npm test -- --run src/features/legal/LegalPages.test.tsx`

Expected: FAIL vì pages chưa tồn tại.

- [ ] **Step 3: Tạo nội dung tĩnh, route và footer**

Không thêm cam kết hoàn tiền/đổi trả chưa có trong requirements; ghi rõ VNPay/MoMo là sandbox trong bản đồ án.

- [ ] **Step 4: Chạy test và commit**

Run: `npm test -- --run src/features/legal/LegalPages.test.tsx src/App.test.tsx`

Expected: PASS.

Commit: `feat(frontend): add legal pages`

### Task FE-7: Gate tích hợp và tài liệu frontend

**Files:**
- Modify: `docs/architecture/sitemap.md`
- Modify: `AptechMart_eProject_Submission/II_eProject_Report/SCREENSHOT_CAPTURE_GUIDE.md`

- [ ] **Step 1: Chạy toàn bộ frontend tests**

Run: `npm test -- --run`

Expected: tất cả test PASS, không có unhandled rejection.

- [ ] **Step 2: Build production**

Run: `npm run build`

Expected: TypeScript và Vite build thành công.

- [ ] **Step 3: Smoke test routes**

Kiểm tra `/reset-password`, URL sai, `/admin`, `/admin/reports/sales`, `/privacy`, `/terms` ở desktop và mobile; xác nhận customer không vào được admin routes.

- [ ] **Step 4: Đồng bộ sitemap và hướng dẫn screenshot**

Chỉ ghi `IMPLEMENTED` cho route đã qua test/build; thêm danh sách screenshot Dashboard, Sales Report, Reset Password và 404.

- [ ] **Step 5: Commit gate**

Commit: `docs: update frontend submission routes`

## Optional FE-8: Support/FAQ gộp

Chỉ thực hiện khi FE-1..FE-7 xanh và còn tối thiểu hai giờ. Tạo `/support`, test nội dung FAQ và liên kết hotline/chi nhánh; nếu không đủ thời gian, bỏ toàn bộ task này, không để route/component dở dang.
