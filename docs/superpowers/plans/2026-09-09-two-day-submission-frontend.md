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
- `GET /api/dev/password-reset-emails/latest?email=<encoded-email>` chỉ tồn tại khi API chạy Development và yêu cầu bearer token Admin (`AdminOnly`); chỉ dùng cho demo/E2E qua API request context Admin riêng, không gọi từ UI production.
- Dashboard `completedRevenue` là tổng toàn thời gian của các đơn Completed.
- Report nhóm theo ngày tạo đơn UTC; preset frontend phải dùng UTC, không dùng ngày local của trình duyệt.

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

- [x] **Step 1: Viết test fail cho nút “Quên mật khẩu?” và submit email**

Mock `requestPasswordResetApi`, mở modal login, click “Quên mật khẩu?”, nhập `user@example.com`, submit, rồi assert thông báo trung tính xuất hiện và không chứa thông tin email tồn tại hay không.

- [x] **Step 2: Chạy test đỏ**

Run: `npm test -- --run src/features/auth/Auth.test.tsx`

Expected: FAIL vì chưa có nút/form/API.

- [x] **Step 3: Thêm API và state `forgot` cho modal**

```ts
export function requestPasswordResetApi(email: string, signal?: AbortSignal) {
  return postJson<{ message: string }>('/auth/password-reset', { email }, { signal })
}
```

Đổi `mode` thành `'login' | 'register' | 'forgot'`; `LoginForm` nhận `onForgotPassword`; `ForgotPasswordForm` validate email rỗng, khóa nút khi gửi và luôn dùng copy thành công: “Nếu email tồn tại, chúng tôi đã gửi liên kết đặt lại mật khẩu.”

- [x] **Step 4: Chạy test xanh và commit**

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

- [x] **Step 1: Viết test fail cho token thiếu, password không khớp và submit thành công**

Test riêng ba case: thiếu token không gọi API; hai password khác nhau báo lỗi client; token hợp lệ gọi API `{ token, newPassword }` và hiển thị nút quay lại đăng nhập/trang chủ.

- [x] **Step 2: Chạy test đỏ**

Run: `npm test -- --run src/features/auth/ResetPasswordPage.test.tsx`

Expected: FAIL vì component chưa tồn tại.

- [x] **Step 3: Cài API, page và route**

```ts
export function confirmPasswordResetApi(token: string, newPassword: string, signal?: AbortSignal) {
  return postJson<{ message: string }>('/auth/password-reset/confirm', { token, newPassword }, { signal })
}
```

Page đọc token bằng `useSearchParams`, có `new-password` và `confirm-password`, `autoComplete="new-password"`, validate rỗng/không khớp, xử lý lỗi 400 thành “Liên kết không hợp lệ hoặc đã hết hạn.”

- [x] **Step 4: Chạy test và commit**

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

- [x] **Step 1: Viết route test fail**

Render app tại `/duong-dan-khong-ton-tai`, assert heading “Không tìm thấy trang”, link `/` và `/products`.

- [x] **Step 2: Chạy test đỏ**

Run: `npm test -- --run src/features/system/NotFoundPage.test.tsx`

Expected: FAIL vì route hiện render shell trống.

- [x] **Step 3: Tạo page và đặt `<Route path="*" element={<NotFoundPage />} />` cuối nhóm route public**

Không redirect tự động; dùng `<Link>` để giữ SPA navigation.

- [x] **Step 4: Chạy test và commit**

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

- [x] **Step 1: Viết test fail cho ready, empty, error/retry và navigation**

Mock `adminApi.getDashboardSummary`. Assert bốn metric với nhãn “Tổng doanh thu đơn hoàn tất”, recent order links, spinner `aria-busy`, error `role="alert"`, retry gọi API lần hai; assert sidebar có “Tổng quan”.

- [x] **Step 2: Chạy test đỏ**

Run: `npm test -- --run src/features/admin/AdminDashboardPage.test.tsx`

Expected: FAIL vì API/page chưa tồn tại.

- [x] **Step 3: Thêm DTO/API và page**

```ts
getDashboardSummary: (token: string, signal?: AbortSignal) =>
  getJson<DashboardSummaryDto>('/admin/dashboard/summary', { token, signal })
```

Page lấy `accessToken`, abort request khi unmount, format tiền bằng `Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })`; recent orders link tới `/admin/orders/:id`.

- [x] **Step 4: Nối route và sidebar**

Thêm `{ to: '/admin/dashboard', label: 'Tổng quan', icon: '📊' }` đầu `NAV_ITEMS`; đổi admin index redirect từ `catalog/categories` sang `dashboard`.

- [x] **Step 5: Chạy test và commit**

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

- [x] **Step 1: Viết test fail cho mặc định 30 ngày, preset, validation và state**

Đóng băng thời gian trong Vitest; dùng ngày UTC và assert lần đầu gửi ngày kết thúc UTC hiện tại cùng ngày bắt đầu 29 ngày trước. Test `from > to` không gọi API, preset 7 ngày tải lại, lỗi có retry. Fixture không có đơn phải có `completedOrderCount: 0` nhưng `daily` vẫn chứa đủ các ngày với `revenue: 0` và `orderCount: 0`; assert zero-state dựa trên count và bảng ngày vẫn render.

- [x] **Step 2: Chạy test đỏ**

Run: `npm test -- --run src/features/admin/AdminSalesReportPage.test.tsx`

Expected: FAIL vì page/API chưa tồn tại.

- [x] **Step 3: Thêm query builder và page**

```ts
getSalesReport: (from: string, to: string, token: string, signal?: AbortSignal) => {
  const query = new URLSearchParams({ from, to })
  return getJson<SalesReportDto>(`/admin/reports/sales?${query}`, { token, signal })
}
```

Dùng `<input type="date">`, ba preset 7 ngày/30 ngày/tháng này được tính bằng các helper UTC, ba KPI, average order value và table `date/revenue/orderCount`; không thêm chart library. Hiển thị chú thích “Nhóm theo ngày tạo đơn (UTC)” cạnh bộ lọc.

- [x] **Step 4: Nối route/sidebar, chạy test và commit**

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

- [x] **Step 1: Viết test fail cho routes và footer links**

Assert Privacy mô tả tài khoản, địa chỉ, đơn hàng và thanh toán; Terms mô tả tài khoản, đặt hàng và giới hạn sandbox; footer link dùng route nội bộ.

- [x] **Step 2: Chạy test đỏ**

Run: `npm test -- --run src/features/legal/LegalPages.test.tsx`

Expected: FAIL vì pages chưa tồn tại.

- [x] **Step 3: Tạo nội dung tĩnh, route và footer**

Không thêm cam kết hoàn tiền/đổi trả chưa có trong requirements; ghi rõ VNPay/MoMo là sandbox trong bản đồ án.

- [x] **Step 4: Chạy test và commit**

Run: `npm test -- --run src/features/legal/LegalPages.test.tsx src/App.test.tsx`

Expected: PASS.

Commit: `feat(frontend): add legal pages`

### Task FE-7: Gate tích hợp và tài liệu frontend

**Files:**
- Modify: `docs/architecture/sitemap.md`
- Modify: `AptechMart_eProject_Submission/II_eProject_Report/SCREENSHOT_CAPTURE_GUIDE.md`
- Create: `frontend/playwright.config.ts`
- Create: `frontend/e2e/submission-pages.spec.ts`
- Modify: `frontend/vite.config.ts`

- [x] **Step 1: Tách test runners và tạo E2E suite**

Trong `vite.config.ts`, giới hạn `test.include` thành `['src/**/*.{test,spec}.{ts,tsx}']` để Vitest không thu thập `e2e/submission-pages.spec.ts`. Trong `playwright.config.ts`, đặt `testDir: './e2e'`, cấu hình `baseURL` theo frontend bản nộp (mặc định `http://localhost:5173`). Dùng `test`, `expect` và `defineConfig` từ `playwright/test` đã có trong dependency `playwright`; không thêm package. Tạo suite theo Step 4 trước khi chạy các gate dưới đây.

Chạy các lệnh npm/npx trong thư mục `frontend`; chạy Docker Compose ở repository root. Chuẩn bị Chromium cho Playwright bằng `npx playwright install chromium` nếu máy chưa có browser tương ứng.

Run: `npm test -- --run`

Expected: tất cả test PASS, không có unhandled rejection.

- [x] **Step 2: Build production**

Run: `npm run build`

Expected: TypeScript và Vite build thành công.

- [x] **Step 3: Chạy frontend và API/database thật**

Khởi động stack bản nộp bằng `docker compose up --build -d`, xác nhận API health và frontend tải được. Không thay bằng mock server.

- [x] **Step 4: Chạy E2E contract thật**

Trong `submission-pages.spec.ts`, đọc `E2E_ADMIN_EMAIL`/`E2E_ADMIN_PASSWORD` với mặc định seed `admin@test.com`/`Test@123`. Đăng nhập Admin qua API để tạo API request context riêng có header `Authorization: Bearer <accessToken>`; dùng context này khi gọi report và dev mailbox, không đưa token Admin vào browser context Customer. Tạo Customer riêng cho mỗi run qua register bằng email chứa timestamp và mật khẩu `Test@123`, tránh thay đổi tài khoản seed. Kiểm tra: đăng nhập Admin → mở dashboard → assert dữ liệu seed có recent order và click sang chi tiết; đổi report từ 30 ngày sang 7 ngày → gọi chính API report qua context Admin và đối chiếu KPI tổng tiền; yêu cầu reset Customer vừa tạo → lấy `resetUrl` từ dev mailbox qua context Admin → mở URL trên frontend trong browser context Customer riêng → đặt mật khẩu `Changed@123` → đăng nhập bằng mật khẩu mới. Test cũng kiểm tra URL sai, `/privacy`, `/terms` và customer bị chặn khỏi admin. Không log token/reset URL; không bật trace/video cho luồng reset chứa token.

Run: `npx playwright test e2e/submission-pages.spec.ts`

Expected: PASS với API, MySQL và frontend thật.

- [x] **Step 5: Smoke viewport và đồng bộ tài liệu**

Kiểm tra các route mới ở desktop và mobile viewport. Frontend là owner duy nhất của `docs/architecture/sitemap.md`; chỉ ghi `IMPLEMENTED` cho route đã qua E2E, test và build. Cập nhật hướng dẫn screenshot cho Dashboard, Sales Report, Reset Password và 404.

- [x] **Step 6: Commit gate**

Commit: `docs: update frontend submission routes`

## Optional FE-8: Support/FAQ gộp

Chỉ thực hiện khi FE-1..FE-7 xanh và còn tối thiểu hai giờ. Tạo `/support`, test nội dung FAQ và liên kết hotline/chi nhánh; nếu không đủ thời gian, bỏ toàn bộ task này, không để route/component dở dang.
