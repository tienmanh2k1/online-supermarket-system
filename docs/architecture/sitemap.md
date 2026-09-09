# Sitemap Hệ Thống AptechMart - Siêu Thị Điện Tử Trực Tuyến

Trạng thái: **OFFICIAL (Canonical) — RELEASE READY (Gate A & Gate B Verified)**  
Ngày cập nhật: 2026-09-08  
Phạm vi: Cấu trúc điều hướng giao diện React Storefront & Admin Portal thực tế (khớp 100% với `frontend/src/App.tsx`).  
Liên kết: Traceability tới các Yêu cầu Chức năng (FR-101 đến FR-209) và Sơ đồ Luồng Dữ liệu (DFD P.1 đến P.12).

---

## 1. Quy Ước Ký Hiệu & Điều Kiện Truy Cập

- **Actor (Tác nhân)**:
  - `Guest`: Khách vãng lai chưa đăng nhập.
  - `Customer`: Khách hàng đã đăng nhập bằng tài khoản người dùng hợp lệ.
  - `Admin`: Quản trị viên hệ thống có vai trò `role === "Admin"`.
- **Route Guard (Bảo vệ tuyến đường)**:
  - `Public`: Cho phép mọi tác nhân truy cập tự do.
  - `CustomerRoute`: Yêu cầu phải có Access Token JWT hợp lệ; nếu chưa đăng nhập sẽ kích hoạt `AuthModal`.
  - `AdminRoute`: Kiểm tra vai trò của người dùng; nếu không phải `Admin` sẽ chuyển hướng về `/` (Home) hoặc chặn truy cập.
- **UI States (Trạng thái giao diện)**:
  - `loading`: Hiển thị Skeleton hoặc Spinner trong quá trình nạp dữ liệu bất đồng bộ.
  - `empty`: Hiển thị thông báo khi danh sách rỗng (ví dụ: giỏ hàng trống, chưa có đơn hàng, không tìm thấy sản phẩm).
  - `error`: Bắt lỗi và hiển thị thông báo lỗi thân thiện (kèm nút Retry nếu có).
  - `ready`: Hiển thị đầy đủ nội dung chức năng.

---

## 2. Cây Điều Hướng Tổng Thể (Sitemap Tree)

```
┌─ STOREFRONT PATH (Guest & Customer)
│   │
│   ├─ / (hoặc /browse, /products) ────────── Trang chủ & Duyệt sản phẩm
│   │                                         (Chọn chi nhánh, lọc đa tiêu chí, tìm kiếm,
│   │                                          kệ AI gợi ý trang chủ Home Recommendation)
│   │
│   ├─ /product/:id ───────────────────────── Trang chi tiết sản phẩm
│   │                                         (Thông tin, giá & tồn kho chi nhánh,
│   │                                          kệ gợi ý Similar Products, danh sách Verified Reviews,
│   │                                          nút Thêm vào giỏ & So sánh)
│   │
│   ├─ /branches ──────────────────────────── Danh sách siêu thị / chi nhánh
│   │                                         (Xem thông tin, địa chỉ, hotline, chọn chi nhánh mua sắm)
│   │
│   ├─ /account/profile (hoặc /profile) ──── Hồ sơ cá nhân [Customer Guard]
│   │                                         (Xem thông tin tài khoản, đổi mật khẩu)
│   │
│   ├─ /account/addresses (hoặc /addresses) ─ Sổ địa chỉ giao hàng [Customer Guard]
│   │                                         (Danh sách, Thêm/Sửa/Xóa địa chỉ, Đặt mặc định)
│   │
│   ├─ /shopping/cart ─────────────────────── Giỏ hàng đa chi nhánh
│   │                                         (Quản lý mặt hàng, tăng/giảm số lượng, kiểm tra tồn kho)
│   │
│   ├─ /shopping/checkout ─────────────────── Thanh toán & Đặt hàng [Customer Guard]
│   │                                         (Chọn phương thức nhận: Pickup/Delivery, áp dụng Coupon,
│   │                                          chọn cổng thanh toán: COD, VNPay, MoMo)
│   │
│   ├─ /shopping/checkout/success ─────────── Xác nhận đặt hàng thành công [Customer Guard]
│   │                                         (Mã đơn hàng, chi tiết thanh toán sandbox, hướng dẫn)
│   │
│   ├─ /orders/history ────────────────────── Lịch sử đơn hàng [Customer Guard]
│   │                                         (Danh sách đơn đã đặt, bộ lọc trạng thái đơn)
│   │
│   └─ /orders/history/:id ────────────────── Chi tiết đơn hàng [Customer Guard]
│                                             (Timeline trạng thái đơn, snapshot sản phẩm & địa chỉ,
│                                              nút gửi Đánh giá xác thực cho sản phẩm đã nhận)
│
├─ GLOBAL FLOATING MODALS & WIDGETS
│   │
│   ├─ AuthModal ──────────────────────────── Modal Xác thực người dùng
│   │                                         (Đăng nhập, Đăng ký, Quên mật khẩu, Đặt lại mật khẩu)
│   │
│   └─ CompareModal ───────────────────────── Khay so sánh sản phẩm nổi
│                                             (So sánh thông số, giá bán và đặc tính kỹ thuật)
│
└─ ADMIN PORTAL PATH (/admin - Protected by AdminRoute)
    │
    ├─ /admin ─────────────────────────────── Điều hướng mặc định (Redirect sang /admin/catalog/categories)
    │
    ├─ /admin/catalog/categories ──────────── Quản lý Danh mục sản phẩm (CRUD, phân cấp cây danh mục)
    │
    ├─ /admin/catalog/brands ──────────────── Quản lý Thương hiệu sản phẩm (CRUD, logo, trạng thái)
    │
    ├─ /admin/catalog/products ────────────── Quản lý Thông tin sản phẩm (CRUD, SKU, giá cơ sở, ảnh)
    │
    ├─ /admin/branches ────────────────────── Quản lý Chi nhánh siêu thị (CRUD, hotline, tọa độ GPS)
    │
    ├─ /admin/inventory ───────────────────── Quản lý Tồn kho chi nhánh (Giá bán, On-hand, Sổ cái kho)
    │
    ├─ /admin/orders ──────────────────────── Quản lý Danh sách đơn hàng (Lọc theo chi nhánh, trạng thái)
    │
    ├─ /admin/orders/:id ──────────────────── Quản lý Chi tiết đơn hàng (Cập nhật tiến độ: Confirmed -> Delivered)
    │
    ├─ /admin/promotions ──────────────────── Quản lý Khuyến mãi & Mã Coupon (Thiết lập voucher, hạn mức)
    │
    ├─ /admin/users ───────────────────────── Quản lý Người dùng hệ thống (Xem danh sách, Khóa/Mở khóa)
    │
    ├─ /admin/forecast ────────────────────── Bàn làm việc Dự báo Nhu cầu (Xem dự báo 7-14 ngày, chạy job)
    │
    └─ /admin/recommendations ─────────────── Quản trị Mô hình Gợi ý AI (Trigger huấn luyện, xem mẫu kết quả)
```

---

## 3. Chi Tiết Các Tuyến Đường Storefront (Khách Hàng)

### 3.1. Nhóm Khám Phá & Sản Phẩm (Catalog Browsing)
| Route | Tên Trang / Component | Guard | Chức Năng Chính | API Endpoints Liên Kết |
|---|---|:---:|---|---|
| `/`, `/browse`, `/products` | `ProductBrowsePage` | Public | Xem danh sách sản phẩm phân trang; lọc theo danh mục, thương hiệu, khoảng giá; tìm kiếm theo tên; chọn chi nhánh mua sắm; hiển thị kệ gợi ý sản phẩm trang chủ (`RecommendationShelf`). | `GET /api/products`<br>`GET /api/categories`<br>`GET /api/brands`<br>`GET /api/branches`<br>`GET /api/recommendations/home` |
| `/product/:id` | `ProductDetailPage` | Public | Xem chi tiết sản phẩm, giá bán và tồn kho tại chi nhánh hiện tại; kệ gợi ý sản phẩm tương tự (`Similar Products`); danh sách đánh giá đã xác minh (Verified Reviews); gửi form đánh giá nếu đủ điều kiện mua hàng; nút thêm vào giỏ và so sánh. | `GET /api/products/{id}`<br>`GET /api/recommendations/products/{id}/similar`<br>`GET /api/reviews/products/{id}`<br>`POST /api/reviews`<br>`POST /api/views` |
| `/branches` | `BranchesPage` | Public | Danh sách mạng lưới siêu thị AptechMart; hiển thị địa chỉ, hotline, giờ mở cửa; hỗ trợ khách hàng bấm chọn chi nhánh mua sắm mặc định. | `GET /api/branches` |

### 3.2. Nhóm Tài Khoản & Địa Chỉ (Customer Account)
| Route | Tên Trang / Component | Guard | Chức Năng Chính | API Endpoints Liên Kết |
|---|---|:---:|---|---|
| `/account/profile`, `/profile` | `ProfilePage` | Customer | Xem thông tin tài khoản (Họ tên, email, số điện thoại, vai trò); cập nhật họ tên & số điện thoại; form đổi mật khẩu an toàn. | `GET /api/users/profile`<br>`PUT /api/users/profile`<br>`POST /api/auth/change-password` |
| `/account/addresses`, `/addresses` | `AddressListPage` | Customer | Quản lý sổ địa chỉ giao hàng; thêm địa chỉ mới, chỉnh sửa thông tin giao nhận, xóa địa chỉ; thiết lập địa chỉ nhận hàng mặc định. | `GET /api/addresses`<br>`POST /api/addresses`<br>`PUT /api/addresses/{id}`<br>`DELETE /api/addresses/{id}`<br>`PUT /api/addresses/{id}/default` |

### 3.3. Nhóm Mua Hàng & Thanh Toán (Cart & Checkout)
| Route | Tên Trang / Component | Guard | Chức Năng Chính | API Endpoints Liên Kết |
|---|---|:---:|---|---|
| `/shopping/cart` | `CartPage` | Public / Customer | Xem các mặt hàng trong giỏ gắn với chi nhánh hiện tại; cập nhật số lượng; kiểm tra tồn kho tức thì; xóa sản phẩm; tính toán tổng tiền tạm tính. | `GET /api/cart`<br>`POST /api/cart/items`<br>`PUT /api/cart/items/{id}`<br>`DELETE /api/cart/items/{id}` |
| `/shopping/checkout` | `CheckoutPage` | Customer | Lựa chọn hình thức nhận hàng (Nhận tại siêu thị - Pickup hoặc Giao hàng tận nơi - Delivery); chọn địa chỉ nhận hàng; nhập và áp dụng mã giảm giá (Coupon); chọn phương thức thanh toán (COD, VNPay Sandbox, MoMo Sandbox); khóa giữ tồn kho giao dịch và tạo đơn. | `POST /api/checkout/apply-coupon`<br>`POST /api/checkout`<br>`POST /api/payments/vnpay/create`<br>`POST /api/payments/momo/create` |
| `/shopping/checkout/success` | `CheckoutSuccessPage` | Customer | Màn hình thông báo đặt hàng thành công; hiển thị mã đơn hàng, trạng thái thanh toán và thông tin điều hướng tiếp theo. | `GET /api/orders/{id}` |

### 3.4. Nhóm Đơn Hàng & Lịch Sử (Order Tracking)
| Route | Tên Trang / Component | Guard | Chức Năng Chính | API Endpoints Liên Kết |
|---|---|:---:|---|---|
| `/orders/history` | `OrderHistoryPage` | Customer | Danh sách toàn bộ đơn hàng của người dùng; lọc đơn theo trạng thái (`Pending`, `Confirmed`, `Shipping`, `Delivered`, `Cancelled`); hiển thị ngày đặt và tổng tiền. | `GET /api/orders` |
| `/orders/history/:id` | `OrderDetailPage` | Customer | Chi tiết đơn hàng: dòng thời gian tiến độ xử lý đơn; snapshot thông tin người nhận và địa chỉ; danh sách các mặt hàng đã mua kèm đơn giá snapshot; nút viết đánh giá cho từng sản phẩm đã giao. | `GET /api/orders/{id}`<br>`POST /api/reviews` |

---

## 4. Chi Tiết Các Tuyến Đường Admin Portal (Quản Trị Viên)

Tất cả các tuyến đường quản trị đều nằm dưới tiền tố `/admin` và được bảo vệ nghiêm ngặt bởi thành phần `AdminRoute` (`role === "Admin"`).

| Route | Tên Trang / Component | Chức Năng Quản Trị Chi Tiết | API Endpoints Liên Kết |
|---|---|---|---|
| `/admin/catalog/categories` | `AdminCategoriesPage` | Quản lý danh mục sản phẩm đa cấp: xem cây danh mục, thêm mới danh mục, chỉnh sửa tên, cấu hình danh mục cha, kích hoạt hoặc ẩn danh mục. | `GET /api/admin/categories`<br>`POST /api/admin/categories`<br>`PUT /api/admin/categories/{id}` |
| `/admin/catalog/brands` | `AdminBrandsPage` | Quản lý thương hiệu đối tác: danh sách thương hiệu, thêm thương hiệu mới, sửa đổi thông tin, ẩn/hiện thương hiệu trên Storefront. | `GET /api/admin/brands`<br>`POST /api/admin/brands`<br>`PUT /api/admin/brands/{id}` |
| `/admin/catalog/products` | `AdminProductsPage` | Quản lý kho sản phẩm toàn hệ thống: thêm mới sản phẩm, cập nhật mã SKU, giá bán cơ sở, đơn vị tính, chọn danh mục lá, upload URL ảnh sản phẩm. | `GET /api/admin/products`<br>`POST /api/admin/products`<br>`PUT /api/admin/products/{id}` |
| `/admin/branches` | `AdminBranchesPage` | Quản lý danh sách chi nhánh: thêm chi nhánh mới, cập nhật địa chỉ, số điện thoại hotline, tọa độ kinh độ/vĩ độ và trạng thái hoạt động. | `GET /api/admin/branches`<br>`POST /api/admin/branches`<br>`PUT /api/admin/branches/{id}` |
| `/admin/inventory` | `AdminInventoryPage` | Quản lý tồn kho đa chi nhánh: lọc sản phẩm theo chi nhánh, điều chỉnh giá bán `selling_price`, cập nhật số lượng tồn kho thực tế, định mức nhập hàng; xem lịch sử giao dịch sổ cái kho (`inventory_transactions`). | `GET /api/admin/branches/{id}/inventory`<br>`PUT /api/admin/branches/{id}/inventory/{productId}`<br>`GET /api/admin/inventory/transactions` |
| `/admin/orders` | `AdminOrdersPage` | Quản lý danh sách đơn hàng toàn hệ thống: lọc theo chi nhánh siêu thị thực hiện đơn, lọc theo trạng thái đơn hàng, tìm kiếm theo mã đơn hoặc người mua. | `GET /api/admin/orders` |
| `/admin/orders/:id` | `AdminOrderDetailPage` | Chi tiết và xử lý đơn hàng: xem thông tin thanh toán, chuyển trạng thái đơn hàng tuần tự (`Confirmed` -> `Preparing` -> `Shipping` -> `Delivered`); hủy đơn hàng và tự động kích hoạt hoàn kho qua sổ cái. | `GET /api/admin/orders/{id}`<br>`PUT /api/admin/orders/{id}/status` |
| `/admin/promotions` | `AdminPromotionsPage` | Quản lý chương trình khuyến mãi: tạo mã giảm giá (giảm theo % hoặc số tiền cố định), thiết lập hạn mức đơn tối thiểu, số lượng sử dụng tối đa, kích hoạt hoặc tạm dừng áp dụng. | `GET /api/admin/promotions`<br>`POST /api/admin/promotions`<br>`PUT /api/admin/promotions/{id}/status` |
| `/admin/users` | `AdminUsersPage` | Quản lý tài khoản người dùng: tra cứu danh sách khách hàng và quản trị viên; khóa tài khoản có dấu hiệu vi phạm hoặc mở khóa tài khoản; tự động thu hồi phiên đăng nhập khi bị khóa. | `GET /api/admin/users`<br>`PUT /api/admin/users/{id}/status` |
| `/admin/forecast` | `AdminForecastPage` | Bàn làm việc dự báo nhu cầu hàng hóa: chọn chi nhánh siêu thị, xem biểu đồ dự báo tiêu thụ cho 7 ngày và 14 ngày tới; kích hoạt chạy job tính toán lại dự báo nhu cầu. | `GET /api/admin/forecasts`<br>`POST /api/admin/jobs/forecast/runs` |
| `/admin/recommendations` | `AdminRecommendationsPage` | Bảng điều khiển mô hình gợi ý AI: kích hoạt chạy batch huấn luyện mô hình ML.NET Matrix Factorization; polling theo dõi tiến độ thời gian thực; kiểm tra dữ liệu gợi ý mẫu phân biệt rõ phiên bản `mf-v1` và fallback `content-v1`. | `POST /api/admin/jobs/recommendations/runs`<br>`GET /api/admin/jobs/recommendations/runs/{id}`<br>`GET /api/admin/recommendations/samples` |

---

## 5. Bảng Đối Soát Traceability (Route -> Yêu Cầu Chức Năng FR -> Component)

| Yêu Cầu Chức Năng | Tên Tính Năng Nghiệp Vụ | Tuyến Đường (Route) Giao Diện | React Component Hiện Thực |
|---|---|---|---|
| **FR-101** | Tìm kiếm, lọc và phân trang sản phẩm | `/`, `/browse`, `/products` | `ProductBrowsePage` |
| **FR-102** | Xem thông tin chi tiết sản phẩm | `/product/:id` | `ProductDetailPage` |
| **FR-103** | Lựa chọn chi nhánh và hiển thị tồn kho/giá | `/branches`, `/` | `BranchesPage`, `BranchSelectorModal` |
| **FR-104** | So sánh thông số sản phẩm trực quan | Toàn hệ thống (Floating) | `CompareModal` |
| **FR-105** | Quản lý hồ sơ và đổi mật khẩu | `/account/profile`, `/profile` | `ProfilePage` |
| **FR-106** | Quản lý sổ địa chỉ giao hàng | `/account/addresses`, `/addresses` | `AddressListPage` |
| **FR-107** | Giỏ hàng trực tuyến theo chi nhánh | `/shopping/cart` | `CartPage` |
| **FR-108** | Quy trình đặt hàng an toàn (Checkout) | `/shopping/checkout` | `CheckoutPage` |
| **FR-109** | Theo dõi lịch sử và chi tiết đơn hàng | `/orders/history`, `/orders/history/:id` | `OrderHistoryPage`, `OrderDetailPage` |
| **FR-110** | Thanh toán trực tuyến (COD, VNPay, MoMo) | `/shopping/checkout`, `/shopping/checkout/success` | `CheckoutPage`, `CheckoutSuccessPage` |
| **FR-111** | Áp dụng mã giảm giá (Coupon) khi checkout | `/shopping/checkout` | `CheckoutPage` |
| **FR-112** | Hủy đơn hàng và tự động giải phóng tồn kho | `/orders/history/:id` | `OrderDetailPage` |
| **FR-113** | Đánh giá sản phẩm đã mua (Verified Reviews) | `/product/:id`, `/orders/history/:id` | `ProductDetailPage`, `ReviewFormModal` |
| **FR-114** | Đăng ký tài khoản khách hàng mới | Toàn hệ thống (Auth Header) | `AuthModal` (Tab Register) |
| **FR-115** | Đăng nhập JWT và khôi phục mật khẩu | Toàn hệ thống (Auth Header) | `AuthModal` (Tab Login / Forgot) |
| **FR-201** | Quản trị Danh mục và Thương hiệu | `/admin/catalog/categories`, `/admin/catalog/brands` | `AdminCategoriesPage`, `AdminBrandsPage` |
| **FR-202** | Quản trị Thông tin sản phẩm | `/admin/catalog/products` | `AdminProductsPage` |
| **FR-203** | Quản trị Tồn kho và Chi nhánh siêu thị | `/admin/branches`, `/admin/inventory` | `AdminBranchesPage`, `AdminInventoryPage` |
| **FR-204** | Quản trị Chiến dịch Khuyến mãi & Voucher | `/admin/promotions` | `AdminPromotionsPage` |
| **FR-205** | Quản lý và xử lý Đơn hàng (Admin) | `/admin/orders`, `/admin/orders/:id` | `AdminOrdersPage`, `AdminOrderDetailPage` |
| **FR-206** | Quản trị và phân quyền Người dùng | `/admin/users` | `AdminUsersPage` |
| **FR-207** | Báo cáo phân tích và thống kê vận hành | `/admin/inventory`, `/admin/orders` | `AdminInventoryPage`, `AdminOrdersPage` |
| **FR-208** | Gợi ý sản phẩm cá nhân hóa thông minh (AI) | `/`, `/product/:id`, `/admin/recommendations` | `RecommendationShelf`, `AdminRecommendationsPage` |
| **FR-209** | Dự báo nhu cầu hàng hóa chi nhánh (Forecast) | `/admin/forecast` | `AdminForecastPage` |
