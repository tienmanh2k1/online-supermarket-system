# Sơ Đồ Luồng Dữ Liệu (DFD) Hệ Thống AptechMart

Trạng thái: **OFFICIAL (Canonical) — RELEASE READY (Gate A & Gate B Verified)**  
Ngày cập nhật: 2026-09-08  
Phạm vi: **23 Bảng Vật Lý**, 12 Phân hệ Quy trình (P.1 đến P.12) hoàn thành 100%.  
Cấu trúc tài liệu: Context Diagram → Level 0 DFD → Level 1 DFD (Chi tiết) → Bảng Cân bằng I/O & Traceability.

---

## 1. Quy Ước Ký Hiệu

- **External Entity (E)**: Tác nhân bên ngoài tương tác với hệ thống:
  - `E1`: Khách vãng lai (Guest)
  - `E2`: Khách hàng đã đăng ký (Customer)
  - `E3`: Quản trị viên hệ thống (Admin)
  - `E4`: Cổng thanh toán trực tuyến (VNPay Sandbox, MoMo Sandbox, COD Processor)
  - `E5`: Bộ lập lịch tác vụ nền (Background Job Scheduler / Timer)
- **Process (P)**: Các quy trình xử lý dữ liệu (Mức 0: `P.1` .. `P.12`; Mức 1: `P.N.M`).
- **Data Store (D)**: Kho dữ liệu cơ sở dữ liệu MySQL (gồm 9 nhóm lưu trữ ánh xạ 23 bảng vật lý):
  - `D1` (Catalog): `CATEGORIES`, `BRANDS`, `PRODUCTS`
  - `D2` (Identity & Security): `USERS`, `REFRESH_TOKENS`, `PASSWORD_RESET_TOKENS`, `ADDRESSES`
  - `D3` (Branch & Inventory): `BRANCHES`, `BRANCH_INVENTORIES`, `INVENTORY_TRANSACTIONS`
  - `D4` (Shopping Cart): `CARTS`, `CART_ITEMS`
  - `D5` (Orders): `ORDERS`, `ORDER_ITEMS`, `ORDER_STATUS_HISTORIES`
  - `D6` (Payments): `PAYMENTS`, `PAYMENT_CALLBACKS`
  - `D7` (Promotions): `PROMOTIONS`
  - `D8` (Customer Reviews): `REVIEWS`
  - `D9` (Intelligence & Jobs): `PRODUCT_VIEW_EVENTS`, `BACKGROUND_JOB_RUNS`, `RECOMMENDATION_RESULTS`, `DEMAND_FORECASTS`
- **Data Flow (→)**: Luồng dữ liệu trao đổi giữa các thực thể, tiến trình và kho lưu trữ.

---

## 2. Sơ Đồ Ngữ Cảnh (Context Diagram)

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                    AptechMart - Siêu thị Điện tử Đa Chi nhánh                   │
│                                                                                 │
│                              [Hệ Thống Phần Mềm]                                │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
       ↑                    ↑                    ↑                  ↑           ↑
       │                    │                    │                  │           │
     Guest               Customer              Admin             Payment    Scheduler
  (Duyệt hàng)      (Đặt hàng, Đánh giá)    (Quản trị hệ thống)  Provider   (Job Trigger)
  (Theo dõi view)   (Thanh toán, Hồ sơ)     (Kho, Catalog, AI)   (VNPay/MoMo)
```

### Mô tả luồng tương tác chính:
- **Guest (E1)**: Chọn chi nhánh, duyệt danh mục, tìm kiếm sản phẩm, xem tồn kho/giá bán tại chi nhánh; phát sinh sự kiện xem ẩn danh (`anonymous_session_id`).
- **Customer (E2)**: Đăng ký, đăng nhập (tự động merge session ẩn danh), quản lý địa chỉ, giỏ hàng, áp dụng coupon, checkout (khóa tồn kho), thanh toán sandbox, theo dõi lịch sử đơn hàng, gửi đánh giá sản phẩm đã mua thành công.
- **Admin (E3)**: Quản lý danh mục lá, thương hiệu, sản phẩm; điều chỉnh tồn kho chi nhánh (ghi sổ cái bất biến); quản lý khuyến mãi; cập nhật trạng thái đơn; khóa/mở khóa người dùng; xem dự báo nhu cầu và trigger chạy mô hình gợi ý AI.
- **Payment Provider (E4)**: Nhận yêu cầu thanh toán, điều hướng sandbox và gửi webhook/IPN callback có chữ ký số xác nhận kết quả thanh toán.
- **Background Scheduler (E5)**: Kích hoạt định kỳ các tác vụ nền: tính toán dự báo nhu cầu chi nhánh (Demand Forecast) và huấn luyện mô hình máy học (ML.NET Matrix Factorization).

---

## 3. Sơ Đồ Mức 0 (Level 0 DFD)

```
            Guest (E1)
              │
              ├─→ [P.1] Duyệt hàng & Tìm kiếm ──→ [D1] Catalog & [D3] Inventory
              │
            Customer (E2)
              │
              ├─→ [P.2] Quản lý tài khoản, Địa chỉ & Session Merge ──→ [D2] Identity & [D9] View Events
              │
              ├─→ [P.3] Quản lý giỏ hàng theo chi nhánh ──→ [D4] Cart & [D3] Inventory
              │
              ├─→ [P.4] Checkout & Khóa giữ tồn kho giao dịch ──→ [D4], [D5] Orders, [D7] Promo & [D3] Ledger
              │
              ├─→ [P.5] Thanh toán & Xử lý Callback IPN ──→ [D6] Payments & [E4] Payment Provider
              │                                                ↑
              │                                           Callback IPN
              │
              └─→ [P.6] Đánh giá & Bình luận xác thực ──→ [D8] Reviews (Đối soát [D5] OrderItems)

            Admin (E3)
              │
              ├─→ [P.7] Quản lý Catalog (Category, Brand, Product) ──→ [D1] Catalog
              │
              ├─→ [P.8] Quản lý Chi nhánh & Sổ cái Tồn kho ──→ [D3] Branch & Inventory Ledger
              │
              ├─→ [P.9] Quản lý Khuyến mãi & Coupon ──→ [D7] Promotions
              │
              ├─→ [P.10] Quản lý Đơn hàng & Chuyển trạng thái ──→ [D5] Orders & [D3] Ledger
              │
              ├─→ [P.11] Quản lý Người dùng & Phân quyền ──→ [D2] Identity
              │
              └─→ [P.12] Phân hệ AI Gợi ý & Dự báo Nhu cầu ──→ [D9] Intelligence & Background Jobs
                                                                    ↑
                                                              Scheduler (E5)
```

---

## 4. Sơ Đồ Mức 1: Chi Tiết Từng Quy Trình (Level 1 DFD)

### P.1 — Duyệt hàng & Tìm kiếm (Guest / Customer)
- **P.1.1 Chọn chi nhánh**: Đọc danh sách chi nhánh hoạt động từ `[D3] BRANCHES`, lưu chi nhánh hoạt động vào context người dùng.
- **P.1.2 Tìm kiếm & Lọc sản phẩm**: Truy vấn `[D1] CATEGORIES, BRANDS, PRODUCTS` theo từ khóa, ngành hàng phân cấp, thương hiệu.
- **P.1.3 Truy vấn tồn kho & giá bán chi nhánh**: Nối bảng `[D3] BRANCH_INVENTORIES` để hiển thị đúng `selling_price` và `available_quantity` của chi nhánh đã chọn.
- **P.1.4 Hiển thị kệ gợi ý sản phẩm**: Đọc danh sách gợi ý phù hợp từ `[D9] RECOMMENDATION_RESULTS` (User scope cho khách đăng nhập, Global scope cho khách mới).

### P.2 — Quản lý Tài khoản, Sổ Địa chỉ & Hợp nhất Session (Customer / Admin)
- **P.2.1 Đăng ký & Xác thực JWT**: Băm mật khẩu bằng PBKDF2/BCrypt, phát hành Access Token và lưu Refresh Token xoay vòng vào `[D2] REFRESH_TOKENS`.
- **P.2.2 Quên mật khẩu & Khôi phục**: Tạo mã token khôi phục dùng 1 lần trong `[D2] PASSWORD_RESET_TOKENS`.
- **P.2.3 CRUD Sổ địa chỉ giao hàng**: Cập nhật thông tin giao nhận vào `[D2] ADDRESSES`, xử lý cờ `is_default` transactional.
- **P.2.4 Hợp nhất Session Xem Ẩn danh (Session Merge)**: Khi đăng nhập thành công, gọi API gộp toàn bộ sự kiện xem có `anonymous_session_id` trong `[D9] PRODUCT_VIEW_EVENTS` sang `user_id` của khách hàng mà không làm mất lịch sử tương tác.

### P.3 — Quản lý Giỏ hàng Đa Chi nhánh (Customer)
- **P.3.1 Khởi tạo / Truy vấn giỏ hàng**: Lấy giỏ hàng theo cặp `(user_id, branch_id)` từ `[D4] CARTS`.
- **P.3.2 Thêm sản phẩm vào giỏ**: Kiểm tra tồn kho khả dụng tức thời từ `[D3] BRANCH_INVENTORIES`, tạo hoặc cộng dồn số lượng trong `[D4] CART_ITEMS`.
- **P.3.3 Cập nhật số lượng / Xóa khỏi giỏ**: Kiểm tra điều kiện tồn kho trước khi tăng số lượng; cập nhật dòng hàng trong `[D4] CART_ITEMS`.
- **P.3.4 Chuyển đổi chi nhánh mua sắm**: Re-validate toàn bộ giỏ hàng đối chiếu với tồn kho chi nhánh mới; cảnh báo hoặc loại bỏ các mặt hàng không đủ tồn.

### P.4 — Checkout, Áp dụng Khuyến mãi & Khóa Giữ Tồn Kho (Customer)
- **P.4.1 Kiểm tra & Áp dụng mã giảm giá**: Kiểm tra điều kiện đơn tối thiểu, hạn sử dụng và số lượt còn lại từ `[D7] PROMOTIONS`, tính toán số tiền giảm.
- **P.4.2 Khóa giữ tồn kho giao dịch (Stock Reservation)**: Mở Database Transaction, cập nhật tăng `reserved_quantity` trong `[D3] BRANCH_INVENTORIES` với điều kiện `available_quantity >= quantity` (chống Race Condition).
- **P.4.3 Ghi Sổ cái biến động kho bất biến**: Thêm bản ghi giao dịch giữ hàng vào `[D3] INVENTORY_TRANSACTIONS` với loại `ReservationCreated` và `operation_key` duy nhất.
- **P.4.4 Tạo đơn hàng & Snapshot pháp lý**: Lưu thông tin đơn vào `[D5] ORDERS`, tạo các dòng `[D5] ORDER_ITEMS` kèm snapshot giá bán và tên sản phẩm, lưu lịch sử khởi tạo vào `[D5] ORDER_STATUS_HISTORIES`.
- **P.4.5 Xóa sạch giỏ hàng**: Dọn dẹp các mục trong `[D4] CART_ITEMS` sau khi đơn hàng tạo thành công.

### P.5 — Thanh toán Trực tuyến & Xử lý Callback IPN (Customer ↔ Payment Provider)
- **P.5.1 Khởi tạo giao dịch thanh toán**: Tạo bản ghi trạng thái `Pending` trong `[D6] PAYMENTS`, tạo URL thanh toán VNPay / MoMo Sandbox hoặc xác nhận COD.
- **P.5.2 Tiếp nhận Webhook/IPN Callback**:
  - Nhận request từ cổng thanh toán, kiểm tra chữ ký số HMAC (SHA512 với VNPay / SHA256 với MoMo).
  - Kiểm tra tính duy nhất (Idempotency) qua `external_event_id` trong `[D6] PAYMENT_CALLBACKS`.
- **P.5.3 Xử lý kết quả thanh toán**:
  - *Nếu thành công*: Cập nhật `[D6] PAYMENTS` sang `Completed`, cập nhật `[D5] ORDERS` sang `Confirmed`, chuyển tồn kho từ `reserved` sang xuất kho bán (`Sale`) trên `[D3] INVENTORY_TRANSACTIONS`.
  - *Nếu thất bại / Hủy bỏ*: Cập nhật đơn sang `Cancelled`, tự động hoàn trả tồn kho (`ReleaseReservation`) trên `[D3] BRANCH_INVENTORIES` và ghi sổ cái `[D3] INVENTORY_TRANSACTIONS`.

### P.6 — Đánh giá & Bình luận Xác thực (Customer)
- **P.6.1 Kiểm tra điều kiện mua hàng (Verified Purchase Eligibility)**:
  - Kiểm tra người dùng có đơn hàng chứa sản phẩm ở trạng thái `Delivered` từ `[D5] ORDERS` và `[D5] ORDER_ITEMS`.
  - Kiểm tra `order_item_id` này chưa từng được đánh giá trước đó (ràng buộc 1:1).
- **P.6.2 Lưu đánh giá**: Ghi điểm số rating (1 đến 5) và bình luận vào `[D8] REVIEWS`.

### P.7 — Quản trị Catalog Sản phẩm (Admin)
- **P.7.1 Quản lý Danh mục (Categories)**: CRUD danh mục, tự động tạo slug chuẩn hóa, ràng buộc phân cấp cha-con, quy định sản phẩm chỉ gán vào danh mục lá.
- **P.7.2 Quản lý Thương hiệu (Brands)**: CRUD thương hiệu, quản lý logo và trạng thái hoạt động.
- **P.7.3 Quản lý Sản phẩm (Products)**: CRUD thông tin sản phẩm, quản lý mã SKU duy nhất, đơn vị tính, giá cơ sở và upload hình ảnh.

### P.8 — Quản trị Chi nhánh, Tồn kho & Sổ cái Kiểm toán (Admin)
- **P.8.1 Quản lý Chi nhánh**: CRUD thông tin chi nhánh, địa chỉ, số điện thoại, tọa độ địa lý phục vụ định vị siêu thị.
- **P.8.2 Điều chỉnh Tồn kho & Giá bán**: Cập nhật giá bán `selling_price`, số lượng thực tế `quantity_on_hand` và định mức tồn kho tối thiểu `reorder_level` theo từng chi nhánh.
- **P.8.3 Kiểm toán Sổ cái Giao dịch kho**: Mọi thao tác điều chỉnh kiểm kê đều bắt buộc ghi bản ghi audit vào `[D3] INVENTORY_TRANSACTIONS` kèm `actor_user_id` và ghi chú giải trình.

### P.9 — Quản trị Khuyến mãi & Coupon (Admin)
- **P.9.1 Thiết lập chương trình khuyến mãi**: Tạo mã giảm giá trong `[D7] PROMOTIONS` (loại giảm: % hoặc số tiền cố định, giá trị giảm, đơn hàng tối thiểu, giới hạn số lượt).
- **P.9.2 Quản lý hiệu lực**: Kích hoạt hoặc vô hiệu hóa mã khuyến mãi tức thì (`is_active`).

### P.10 — Quản trị Đơn hàng & Vòng đời Xử lý (Admin)
- **P.10.1 Quản lý danh sách đơn hàng**: Xem và lọc đơn hàng theo chi nhánh, khoảng thời gian và trạng thái xử lý.
- **P.10.2 Cập nhật trạng thái đơn hàng**: Chuyển trạng thái tuần tự (`Confirmed` -> `Preparing` -> `Shipping` -> `Delivered`); ghi nhật ký chi tiết vào `[D5] ORDER_STATUS_HISTORIES`.
- **P.10.3 Xử lý Hủy đơn & Hoàn kho**: Khi đơn bị hủy, tự động hoàn trả số lượng hàng về tồn kho khả dụng thông qua giao dịch `[D3] INVENTORY_TRANSACTIONS`.

### P.11 — Quản trị Người dùng & Phân quyền (Admin)
- **P.11.1 Xem danh sách người dùng**: Tra cứu tài khoản theo email, số điện thoại, vai trò (`Customer`, `Admin`).
- **P.11.2 Khóa / Mở khóa tài khoản**: Cập nhật cờ `status` trong `[D2] USERS` (`Active`, `Locked`, `Disabled`) và tự động thu hồi Refresh Token tương ứng để chặn truy cập ngay lập tức.

### P.12 — Phân hệ AI Gợi ý & Dự báo Nhu cầu (Intelligence & Background Jobs)
- **P.12.1 Thu thập sự kiện xem sản phẩm**: Tiếp nhận sự kiện xem từ Storefront, ghi nhận vào `[D9] PRODUCT_VIEW_EVENTS` (lưu kèm `product_id`, `user_id` hoặc `anonymous_session_id`, `branch_id`).
- **P.12.2 Quản lý vòng đời tác vụ nền (Job Infrastructure)**:
  - `[D9] BACKGROUND_JOB_RUNS` quản lý trạng thái các lượt chạy (`Queued`, `Running`, `Succeeded`, `Failed`).
  - Hỗ trợ cả kích hoạt tự động theo lịch (Scheduler) và kích hoạt thủ công từ Admin UI qua API.
- **P.12.3 Huấn luyện & Phát hành Gợi ý AI (Recommendation Pipeline)**:
  - Tổng hợp ma trận tương tác (Lượt xem + Lịch sử mua hàng).
  - Khi đủ dữ liệu (>= 5 tương tác): Huấn luyện mô hình ML.NET Matrix Factorization, phát hành gợi ý cá nhân hóa `mf-v1`.
  - Khi dữ liệu thưa hoặc người dùng mới (Cold-Start): Tự động fallback an toàn sang thuật toán Content-Based / Popularity `content-v1`.
  - Loại trừ nghiêm ngặt các sản phẩm đã xem/mua hoặc sản phẩm hết hàng tại chi nhánh.
  - Vật chất hóa kết quả lưu vào `[D9] RECOMMENDATION_RESULTS` theo các phạm vi `User`, `Global`, `SimilarProduct`.
- **P.12.4 Dự báo nhu cầu hàng hóa chi nhánh (Demand Forecasting)**:
  - Phân tích chuỗi dữ liệu bán hàng lịch sử theo từng sản phẩm tại từng chi nhánh.
  - Tính toán số lượng dự báo tiêu thụ cho chu kỳ 7 ngày và 14 ngày tới.
  - Lưu kết quả vào `[D9] DEMAND_FORECASTS` (kèm đánh giá chất lượng dữ liệu `data_quality`).

---

## 5. Bảng Cân Bằng Input/Output & Traceability Yêu Cầu

| Process ID | Tên Quy Trình | Yêu Cầu Ánh Xạ (FR / SD) | Dữ Liệu Đầu Vào (Input) | Dữ Liệu Đầu Ra (Output) | Data Stores Sử Dụng |
|---|---|---|---|---|---|
| **P.1** | Duyệt hàng & Tìm kiếm | FR-101, FR-102, FR-103, FR-104 | Từ khóa, Danh mục, Chi nhánh | Danh sách sản phẩm, giá bán, tồn kho khả dụng | D1, D3, D9 |
| **P.2** | Tài khoản, Địa chỉ & Session Merge | FR-105, FR-106, FR-114, FR-115, FR-206 | Credentials, Sổ địa chỉ, Session ID | JWT Access Token, User Profile, Merged Events | D2, D9 |
| **P.3** | Quản lý Giỏ hàng Đa Chi nhánh | FR-107 | Thao tác giỏ, BranchId | Giỏ hàng chi nhánh, Tạm tính, Kiểm tra tồn | D3, D4 |
| **P.4** | Checkout & Khóa Tồn kho Giao dịch | FR-108, FR-109, FR-111 | Giỏ hàng, Thông tin nhận, Mã coupon | Đơn hàng mới, Tồn kho Reserved, Sổ cái kho | D3, D4, D5, D7 |
| **P.5** | Thanh toán Trực tuyến & Callback IPN | FR-110, FR-112 | Lựa chọn cổng, Webhook IPN Callback | Bản ghi thanh toán, Trạng thái đơn xác nhận | D3, D5, D6 |
| **P.6** | Đánh giá Sản phẩm Xác thực | FR-113 | Đơn hàng đã giao, Số sao (1-5), Bình luận | Bản ghi đánh giá đã duyệt, Điểm uy tín | D5, D8 |
| **P.7** | Quản trị Catalog Sản phẩm | FR-201, FR-202 | Dữ liệu danh mục, thương hiệu, sản phẩm | Cây danh mục, Thương hiệu, Sản phẩm CRUD | D1 |
| **P.8** | Quản trị Chi nhánh & Sổ cái Tồn kho | FR-203 | Điều chỉnh tồn kho, Giá bán chi nhánh | Tồn kho cập nhật, Nhật ký kiểm toán kho | D3 |
| **P.9** | Quản trị Khuyến mãi & Coupon | FR-204 | Quy tắc giảm giá, Mã voucher, Hạn dùng | Mã khuyến mãi hoạt động, Báo cáo sử dụng | D7 |
| **P.10** | Quản trị Đơn hàng & Vòng đời Xử lý | FR-205 | Cập nhật trạng thái đơn (Admin) | Lịch sử trạng thái đơn, Hoàn kho khi hủy | D3, D5 |
| **P.11** | Quản trị Người dùng & Phân quyền | FR-206 | Lệnh khóa/mở khóa, Thay đổi vai trò | Trạng thái tài khoản, Thu hồi session token | D2 |
| **P.12** | Phân hệ AI Gợi ý & Dự báo Nhu cầu | FR-207, FR-208, FR-209 | Sự kiện xem, Đơn hàng lịch sử, Lệnh trigger | Model MF `mf-v1`, Fallback `content-v1`, Forecast | D3, D5, D9 |

---

## 6. Bảng Trạng Thái Triển Khai Thực Tế

| Phân hệ / Quy Trình | Trạng Thái Kiểm Chứng | Các Bảng / Thực Thể Tham Gia | Cột Mốc Nghiệm Thu |
|---|:---:|---|:---:|
| **P.1 Duyệt hàng & Tìm kiếm** | ✅ **Đã hoàn thành** | `Category`, `Brand`, `Product`, `Branch`, `BranchInventory` | Gate A Pass |
| **P.2 Quản lý Tài khoản & Session Merge** | ✅ **Đã hoàn thành** | `User`, `RefreshToken`, `PasswordResetToken`, `Address`, `ProductViewEvent` | Gate A & B Pass |
| **P.3 Quản lý Giỏ hàng Đa Chi nhánh** | ✅ **Đã hoàn thành** | `Cart`, `CartItem`, `BranchInventory` | Gate A Pass |
| **P.4 Checkout & Khóa Tồn kho Giao dịch** | ✅ **Đã hoàn thành** | `Order`, `OrderItem`, `BranchInventory`, `InventoryTransaction`, `Promotion` | Gate A Pass |
| **P.5 Thanh toán Trực tuyến & IPN Callback** | ✅ **Đã hoàn thành** | `Payment`, `PaymentCallback`, `Order`, `InventoryTransaction` | Gate A Pass |
| **P.6 Đánh giá Sản phẩm Xác thực** | ✅ **Đã hoàn thành** | `Review`, `Order`, `OrderItem`, `Product` | Gate A Pass |
| **P.7 Quản trị Catalog Sản phẩm** | ✅ **Đã hoàn thành** | `Category`, `Brand`, `Product` | Gate A Pass |
| **P.8 Quản trị Chi nhánh & Sổ cái Tồn kho** | ✅ **Đã hoàn thành** | `Branch`, `BranchInventory`, `InventoryTransaction` | Gate A Pass |
| **P.9 Quản trị Khuyến mãi & Coupon** | ✅ **Đã hoàn thành** | `Promotion`, `Order` | Gate A Pass |
| **P.10 Quản trị Đơn hàng & Vòng đời** | ✅ **Đã hoàn thành** | `Order`, `OrderItem`, `OrderStatusHistory`, `InventoryTransaction` | Gate A Pass |
| **P.11 Quản trị Người dùng & Phân quyền** | ✅ **Đã hoàn thành** | `User`, `RefreshToken` | Gate A Pass |
| **P.12 Phân hệ AI Gợi ý & Dự báo Nhu cầu** | ✅ **Đã hoàn thành** | `ProductViewEvent`, `BackgroundJobRun`, `RecommendationResult`, `DemandForecast` | Gate B Pass (2026-09-08) |
