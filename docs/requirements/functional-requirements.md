# Danh mục Yêu cầu Chức năng Canonical

Status: **OFFICIAL**

Ngày cập nhật: 2026-09-07
Chu kỳ: Current Release (rút gọn 07–11/09)
Phạm vi: 24 canonical Functional Requirements (FR-101..FR-115, FR-201..FR-209) và 5 Scope Drift items (SD-001..SD-005)
Scope release rút gọn: xem [plan 06/09](../superpowers/plans/2026-09-06-solo-five-day-release-plan.md) và mục 5 ở đây.

> **OFFICIAL:** Tài liệu này là canonical Single Source of Truth (SSoT) cho tất cả FR và SD của dự án. Mọi DFD, sitemap, và project-spec phải tham chiếu bảng này để tránh sai lệch đa nguồn.

## 1. Bảng Registry - Yêu cầu Chức năng (FR)

| ID | Actor | Description | Priority | Status | Acceptance criteria | API | Backlog | Owner | Reviewer |
|---|---|---|---|---|---|---|---|---|---|
| FR-101 | Guest, Customer | Duyệt sản phẩm, tìm kiếm, lọc theo danh mục/thương hiệu/khoảng giá | HIGH | ✅ IMPLEMENTED | Danh sách sản phẩm trả về theo điều kiện lọc; phân trang hoạt động | GET /api/products | DONE | Dev | Rev |
| FR-102 | Guest, Customer | Chọn chi nhánh, xem giá và tồn kho theo chi nhánh | HIGH | ✅ IMPLEMENTED | Giá/tồn kho cập nhật khi đổi chi nhánh; khả dụng = on_hand - reserved | GET /api/branches | DONE | Dev | Rev |
| FR-103 | Guest, Customer | Xem chi tiết sản phẩm, thuộc tính kỹ thuật, hình ảnh | MEDIUM | ✅ IMPLEMENTED | Trang detail hiển thị thông tin, giá/tồn kho chi nhánh hiện tại | GET /api/products/:id | DONE | Dev | Rev |
| FR-104 | Guest, Customer | So sánh tối đa 3–4 sản phẩm cùng danh mục | MEDIUM | DRAFT | Compare table lưu localStorage; reload giá/stock khi đổi branch | GET /api/products | PLANNED_CYCLE_4 | Dev | Rev |
| FR-105 | Customer | Quản lý hồ sơ (tên, email, điện thoại) & đổi mật khẩu | MEDIUM | ✅ IMPLEMENTED | Cập nhật profile, đổi mật khẩu; xác minh quyền ownership | PUT /api/users/me | DONE | Dev | Rev |
| FR-106 | Customer | Quản lý địa chỉ giao hàng (CRUD, đặt mặc định) | MEDIUM | ✅ IMPLEMENTED | Tối đa một địa chỉ mặc định/user; transactional update | POST /api/users/me/addresses | DONE | Dev | Rev |
| FR-107 | Customer | Tạo/cập nhật giỏ hàng per branch | HIGH | ✅ IMPLEMENTED | Giỏ gắn (user, branch); thêm/bớt xác thực stock; đổi branch | POST /api/cart | DONE | Dev | Rev |
| FR-108 | Customer | Checkout: tính lại giá, phí; reserve stock transactional | HIGH | ✅ IMPLEMENTED | Backend tính toán; nếu thiếu → 409; nếu đủ → reserve + create order | POST /api/checkout | DONE | Dev | Rev |
| FR-109 | Customer | Chọn phương thức giao hàng (Pickup/Delivery) | HIGH | ✅ IMPLEMENTED | Pickup: chọn branch; Delivery: chọn address; snapshot lưu order | POST /api/checkout | DONE | Dev | Rev |
| FR-110 | Customer | Chọn phương thức thanh toán (COD, VNPay, MoMo sandbox) | HIGH | ✅ IMPLEMENTED | COD: pending; VNPay/MoMo sandbox return URL; callback IPN | POST /api/checkout/payment | DONE | Dev | Rev |
| FR-111 | Customer | Áp dụng mã khuyến mãi (coupon) | MEDIUM | DRAFT | Validate code; áp dụng discount; kiểm tra usage limit | POST /api/checkout/coupon | PLANNED_CYCLE_5 | Dev | Rev |
| FR-112 | Customer | Xem lịch sử đơn hàng, chi tiết, trạng thái | MEDIUM | ✅ IMPLEMENTED | Danh sách order; filter by status; snapshot địa chỉ, sản phẩm | GET /api/orders | DONE | Dev | Rev |
| FR-113 | Customer | Đánh giá sản phẩm đã mua (1–5 sao + comment) | MEDIUM | ✅ IMPLEMENTED | Order Completed; per item tối đa 1 review; verified purchase enforce | POST /api/reviews | DONE | Dev | Rev |
| FR-114 | Customer | Đăng ký tài khoản bằng email + password | HIGH | ✅ IMPLEMENTED | User tạo; mật khẩu hash an toàn; phân quyền Customer | POST /api/auth/register | DONE | Dev | Rev |
| FR-115 | Customer | Đăng nhập, refresh token, đăng xuất, quên mật khẩu | HIGH | ✅ IMPLEMENTED | JWT access token + refresh token rotation + password reset email | POST /api/auth/login | DONE | Dev | Rev |
| FR-201 | Admin | CRUD danh mục, thương hiệu | MEDIUM | ✅ IMPLEMENTED | Tạo, sửa, xóa (soft); hỗ trợ cây cha-con; slug duy nhất | POST /api/admin/catalog/categories | DONE | Dev | Rev |
| FR-202 | Admin | CRUD sản phẩm, quản lý URL hình ảnh | MEDIUM | ✅ IMPLEMENTED | Tạo, sửa, xóa (soft); SKU duy nhất; image_url lưu ảnh | POST /api/admin/catalog/products | DONE | Dev | Rev |
| FR-203 | Admin | Quản lý chi nhánh, tồn kho, giá per branch | HIGH | ✅ IMPLEMENTED | Quản lý tồn kho & giá chi nhánh; cập nhật stock và reorder level | PUT /api/admin/branches/:branchId/inventory | DONE | Dev | Rev |
| FR-204 | Admin | CRUD khuyến mãi (discount type, value, usage limit, time) | MEDIUM | DRAFT | Tạo, sửa, deactivate; code coupon | POST /api/admin/promotions | PLANNED_CYCLE_5 | Dev | Rev |
| FR-205 | Admin | Quản lý đơn hàng, cập nhật trạng thái, ghi lịch sử | MEDIUM | ✅ IMPLEMENTED | Danh sách, filter; transition status; ghi lịch sử chuyển trạng thái | GET /api/admin/orders | DONE | Dev | Rev |
| FR-206 | Admin | Quản lý người dùng, lock/disable account | MEDIUM | ✅ IMPLEMENTED | Danh sách khách; action lock, disable; role enforcement | GET /api/admin/users | DONE | Dev | Rev |
| FR-207 | Admin | Báo cáo doanh số (ngày, tuần, tháng; by category/brand) | LOW | IN_PROGRESS | Truy vấn Orders + OrderItems; subtotal, discount, fee, total | GET /api/admin/reports/sales | PLANNED_CYCLE_5 | Dev | Rev |
| FR-208 | Admin | Xem dự báo nhu cầu (SMA), báo cáo thiếu dữ liệu; stock alert được hoãn | LOW | ✅ IMPLEMENTED | Demand forecast SMA 7/14; thiếu data → InsufficientData; alert ngoài scope release | GET /api/admin/forecast | DONE | Dev | Rev |
| FR-209 | Customer, Admin | AI Recommendation: một model thật (MF/CF) + fallback rõ ràng | LOW | ✅ IMPLEMENTED | ProductViewEvents logged; model train/infer ra score; cold-start → fallback | GET /api/recommendations | DONE | Dev | Rev |

## 2. Bảng Scope Drift (SD)

| ID | Item | Status | Rule |
|---|---|---|---|
| SD-001 | Wishlist / Saved Items | DEFERRED | Khách có thể lưu sản phẩm để mua sau; chuyển chu kỳ tiếp theo |
| SD-002 | Advanced Product Comparison (5+ items) | OUT_OF_SCOPE | Chỉ so sánh 3–4 sản phẩm trong MVP; expand sau |
| SD-003 | Real-time Shipper Tracking | OUT_OF_SCOPE | Theo dõi shipper thực nằm ngoài phạm vi; ngoài API mô phỏng |
| SD-004 | Warranty & Installation Service | OUT_OF_SCOPE | Quản lý bảo hành, cài đặt, IMEI cắt từ v1; xem xét sau |
| SD-005 | Guest Checkout (COD only) | DEFERRED | Guest có thể thanh toán COD mà không tạo account; hoãn lại |

## 3. Hướng dẫn Sử dụng

### Truy vết Yêu cầu
- Mỗi FR/SD được gán ID ổn định để tham chiếu từ DFD (Process), Sitemap (Route), Project-spec, và code comment.
- Không được dùng ID cũ từ các bản nháp khác.
- Status mỗi dòng phải là `DRAFT`, `PROPOSED`, `APPROVED`, `IMPLEMENTED`, `VERIFIED`, `DEPRECATED`, `IN_PROGRESS`, `DONE`, hoặc `✅ IMPLEMENTED`.
- FR/SD đã implement: đánh dấu `✅ IMPLEMENTED` hoặc `DONE`.

### Priority
- Hỗ trợ: `MUST`, `SHOULD`, `COULD`, `WONT`, `HIGH`, `MEDIUM`, `LOW`, `P0`, `P1`, `P2`, `P3`, `CRITICAL`.
- HIGH/MUST: Checkout, thanh toán, tồn kho, auth, xác thực.
- MEDIUM: Profile, giỏ, đánh giá, quản lý sản phẩm.
- LOW: Báo cáo nâng cao, AI, ngoài MVP.

### API & Backlog Tokens
- `API`: Endpoint GET/POST/PUT/DELETE hoặc token hợp lệ (`DONE`, `PLANNED_CYCLE_4`, `PLANNED_CYCLE_5`, TASK-NNN, #123, URL).
- `Backlog`: Điểm xếp hạng công việc hoặc token kế hoạch; cùng quy tắc token hợp lệ.
- `PLANNED_CYCLE_4`, `PLANNED_CYCLE_5`: Đang nằm trong kế hoạch phát triển các chu kỳ sau.

### Ownership & Review
- Mỗi FR được gán một Owner (dev) và Reviewer (QA/reviewer).
- Quản lý phân công và nghiệm thu kỹ thuật.

### Scope Drift (SD)
- Các yêu cầu nằm ngoài MVP hoặc bị hoãn lại.
- Status: `DEFERRED` (sẽ triển khai sau) hoặc `OUT_OF_SCOPE` (không triển khai trong phạm vi đồ án).

## 4. Tóm tắt Traceability

Dự án Siêu thị Điện tử chứa:
- **24 Functional Requirements** (FR-101 đến FR-209): Xác định chức năng chính cho Guest, Customer, Admin.
- **5 Scope Drift items** (SD-001 đến SD-005): Nhu cầu hoãn lại hoặc ngoài phạm vi.

Tất cả được ánh xạ trong:
- **DFD** (`docs/architecture/dfd.md`): Mỗi Process P.1–P.12 gắn ít nhất một FR hoặc SD.
- **Sitemap** (`docs/architecture/sitemap.md`): Mỗi Route MVP gắn ít nhất một FR; DEFERRED/OUT_OF_SCOPE gắn SD.
- **ERD** (`docs/architecture/erd.md`): Mô hình dữ liệu và các thực thể phục vụ các FR.
- **OpenAPI Contract** (`docs/api/openapi.json`): Đặc tả kỹ thuật các endpoint backend đã triển khai.

## 5. Scope Release Rút Gọn — 07/09/2026

Cập nhật ngày 07/09 theo quyết định của chủ dự án (plan 06/09). Đây là scope của bản nộp release, bổ sung không thay thế registry trên.

- **AI Recommendation (FR-209):** một model thật — Matrix Factorization bằng ML.NET (mục tiêu), item-item collaborative filtering là phương án dự phòng. Fallback `popularity-v1`/`content-v1` giữ làm fallback vận hành, không tính là nghiệm thu AI.
- **Forecast (FR-208):** giữ moving average (SMA), hiển thị đúng tên, không gọi là SSA/AI đã train; không làm stock alert.
- **Jobs/schema:** giữ contract hiện tại (`background_job_runs`, job endpoint 202). Schema thực **23 bảng** (khác spec cũ 22 vì giữ `background_job_runs`); báo cáo ghi đúng 23.
- **Status release của các FR liên quan:**
  - FR-111 (coupon), FR-104 (compare), FR-204 (promotions): vẫn DRAFT — ngoài scope release (không hoàn thành trong đợt này).
  - FR-113 (reviews): ✅ IMPLEMENTED; kiểm chứng 09/09 (completed → tạo/sửa, sai owner, pending, duplicate 409).
  - FR-207 (sales report): IN_PROGRESS — chỉ làm bảng doanh số tối thiểu hoặc ghi deferred có lý do trước EOD 09/09.
  - FR-208, FR-209: ✅ IMPLEMENTED theo scope rút gọn ở trên; gate B 08/09 là điều kiện nghiệm thu AI.
- **Không re-scope tiêu chí:** nếu không hoàn thành trong timebox, ghi blocker; không biến blocker thành deferred. Mọi thay đổi scope khác ghi ở checkpoint ngày.
