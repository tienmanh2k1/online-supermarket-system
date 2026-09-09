# HƯỚNG DẪN CHỤP ẢNH MÀN HÌNH - CHAPTER 4
## AptechMart Multi Branch Online Supermarket System

---

## 1. CHUẨN BỊ TRƯỚC KHI CHỤP

### 1.1. Khởi động ứng dụng

**Phương pháp 1: Docker Compose (Khuyến nghị)**
```bash
cd C:\Users\manh\Documents\Project3\online-supermarket-system
docker compose up --build
```

**Phương pháp 2: Chạy Native**
```bash
# Terminal 1 - Backend
dotnet run --project backend/src/OnlineSupermarket.Api

# Terminal 2 - Frontend
cd frontend
npm run dev
```

### 1.2. Truy cập ứng dụng
- **Storefront**: http://localhost:5173
- **Swagger API**: http://localhost:8080/swagger

### 1.3. Tài khoản Demo

| Role | Email | Password |
|:-----|:------|:---------|
| Admin | admin@test.com | Test@123 |
| Customer 1 | user1@test.com | Test@123 |
| Customer 2 | user2@test.com | Test@123 |

---

## 2. CÀI ĐẶT TRÌNH DUYỆT

### Chrome/Edge - Cài đặt Viewport cố định
1. Mở DevTools (F12)
2. Click icon 📱 (Toggle device toolbar)
3. Chọn **Custom** và nhập:
   - Width: **1920**
   - Height: **1080**
4. Click **Capture full size screenshot** (3 chấm menu)

### Firefox - Screenshot toàn màn hình
1. Mở DevTools (F12)
2. Chuyển sang tab **Inspector**
3. Click chuột phải vào `<body>` → **Screenshot Node**
4. Hoặc dùng: Shift+F2 → `screenshot --fullpage`

### Công cụ Screenshot chuyên dụng

| Tool | OS | Link | Ưu điểm |
|:-----|:---|:-----|:---------|
| **Lightshot** | Windows | lightshot.screenshot | Nhanh, chọn vùng, annotate |
| **Greenshot** | Windows | greenshot.org | Miễn phí, annotate |
| **Snipping Tool** | Windows 10/11 | Tích hợp sẵn | Không cần cài đặt |
| **Xnapper** | macOS | xnapper.com | Đẹp, tự động viền |

---

## 3. HƯỚNG DẪN CHỤP TỪNG HÌNH

### 📸 HÌNH 4.1: STOREFRONT HOMEPAGE
**URL**: http://localhost:5173

**Các bước**:
1. Truy cập trang chủ (chưa đăng nhập)
2. **BẮT BUỘC** hiển thị:
   - Header với Logo "AptechMart"
   - Branch Selector dropdown (ví dụ: "Cau Giay Branch")
   - Thanh tìm kiếm sản phẩm
   - Cart icon với badge
   - Danh mục sản phẩm nổi bật
   - Sản phẩm với **giá bán lẻ theo chi nhánh**
   - Stock status badges

**Lưu ý**: Chọn 1 chi nhánh cụ thể để thấy giá localized

---

### 📸 HÌNH 4.2: CUSTOMER LOGIN
**URL**: http://localhost:5173/login

**Các bước**:
1. Click "Login" trên header
2. Chụp form đăng nhập với:
   - Email input field
   - Password input field
   - "Sign In" button
   - Link "Create Account"
   - Link "Quên mật khẩu?"

---

### 📸 HÌNH 4.2b: KHÔI PHỤC VÀ ĐẶT LẠI MẬT KHẨU
**URL**: http://localhost:5173/reset-password?token=SAMPLE_TOKEN

**Các bước**:
1. Mở liên kết đặt lại mật khẩu từ dev mailbox hoặc click "Quên mật khẩu?"
2. **BẮT BUỘC** hiển thị:
   - Tiêu đề "Đặt lại mật khẩu"
   - Trường nhập mật khẩu mới ("Mật khẩu mới")
   - Trường xác nhận mật khẩu mới ("Xác nhận mật khẩu mới")
   - Nút hành động "Đặt lại mật khẩu"
   - Trạng thái thông báo thành công và liên kết quay về trang chủ

### 📸 HÌNH 4.3: AUTHENTICATED STATE
**URL**: http://localhost:5173 (sau khi đăng nhập)

**Các bước**:
1. Đăng nhập với user1@test.com
2. Chụp header mới với:
   - User avatar/name badge
   - Dropdown menu (My Profile, Orders, Logout)
   - Cart badge đã cập nhật

---

### 📸 HÌNH 4.4: PRODUCT CATALOGUE & FILTERING ⚠️ THIẾU
**URL**: http://localhost:5173/products

**Các bước**:
1. Navigate đến `/products`
2. **BẮT BUỘC** hiển thị:
   - Left sidebar với filters:
     - ✅ Categories (checkboxes)
     - ✅ Brands (checkboxes)
     - ✅ Price range slider
   - Product grid với:
     - Product images
     - Product names
     - **Localized prices** (theo branch đã chọn)
     - Stock badges
3. **Thao tác**: Tick 1-2 filters để thấy products thay đổi
4. Chụp sau khi filter

---

### 📸 HÌNH 4.5: PRODUCT SPECIFICATION DETAIL ⚠️ THIẾU
**URL**: http://localhost:5173/products/{id}

**Các bước**:
1. Click vào 1 sản phẩm bất kỳ
2. **BẮT BUỘC** hiển thị:
   - Product image gallery
   - Product name & brand
   - **Stock availability** (ví dụ: "8 units available at Cau Giay Branch")
   - **Selling price** for active branch
   - Specifications table (JSON rendered)
   - Quantity selector
   - "Add to Cart" button
   - "Compare" button

---

### 📸 HÌNH 4.6: PRODUCT COMPARISON ⚠️ THIẾU
**URL**: Modal popup

**Các bước**:
1. Từ product detail, click "Compare" button
2. Chọn thêm 1-3 sản phẩm cùng category
3. **BẮT BUỘC** hiển thị:
   - Comparison table với aligned columns
   - Technical specifications rows
   - Price comparison
   - Stock status
   - "Add to Cart" links

---

### 📸 HÌNH 4.7: SHOPPING CART ⚠️ THIẾU
**URL**: http://localhost:5173/cart

**Các bước**:
1. Thêm 2-3 sản phẩm vào cart
2. Navigate đến `/cart`
3. **BẮT BUỘC** hiển thị:
   - Cart items với quantities
   - **Branch name** (ví dụ: "Cart for Cau Giay Branch")
   - Per-item prices
   - Subtotal calculation
   - Quantity +/- controls
   - "Remove" buttons
   - "Proceed to Checkout" button

---

### 📸 HÌNH 4.8: CHECKOUT & DELIVERY SELECTION ⚠️ THIẾU
**URL**: http://localhost:5173/checkout

**Các bước**:
1. Từ cart, click "Proceed to Checkout"
2. **BẮT BUỘC** hiển thị:
   - Fulfillment mode tabs:
     - "Home Delivery" (với address selection)
     - "Store Pickup"
   - Coupon code input
   - Order summary với:
     - Subtotal
     - Discount (nếu có)
     - Shipping fee
     - **Total Amount**
   - Payment method selection:
     - COD
     - VNPay Sandbox
     - MoMo Sandbox

---

### 📸 HÌNH 4.9: PAYMENT GATEWAY ⚠️ THIẾU
**URL**: Sau khi chọn VNPay/MoMo

**Các bước**:
1. Chọn VNPay hoặc MoMo
2. Click "Place Order"
3. **BẮT BUỘC** hiển thị:
   - Redirect page (VNPay/MoMo sandbox)
   - Hoặc payment confirmation dialog

---

### 📸 HÌNH 4.10: ORDER HISTORY ⚠️ THIẾU
**URL**: http://localhost:5173/account/orders

**Các bước**:
1. Navigate đến Orders
2. **BẮT BUỘC** hiển thị:
   - Order list (newest first)
   - Order cards với:
     - Order number (ORD-2026-XXXX)
     - Status badge
     - Total amount
     - Date
   - Click 1 order để xem chi tiết:
     - Status timeline (Pending → Confirmed → Processing → Shipped → Completed)
     - Order items với prices
     - Shipping address

---

## 4. ADMIN PORTAL SCREENSHOTS

### 📸 HÌNH 4.11: CATEGORY & BRAND MANAGEMENT ⚠️ THIẾU
**URL**: http://localhost:5173/admin/categories

**Các bước**:
1. Đăng nhập với admin@test.com
2. Navigate đến Admin Portal → Categories
3. **BẮT BUỘC** hiển thị:
   - Multi-level category tree (parent/child)
   - Category CRUD buttons (Add, Edit, Delete)
   - Brand management tab
   - Slug & display order fields

---

### 📸 HÌNH 4.12: PRODUCT MASTER & SKU ⚠️ THIẾU
**URL**: http://localhost:5173/admin/products

**Các bước**:
1. Navigate đến Admin Portal → Products
2. **BẮT BUỘC** hiển thị:
   - Product list/table
   - "Create Product" button
   - Product form với:
     - Name, SKU field
     - Category & Brand dropdowns
     - Unit of measure
     - Specifications (JSON)
     - Image URLs

---

### 📸 HÌNH 4.13: BRANCH INVENTORY & PRICING ⚠️ THIẾU
**URL**: http://localhost:5173/admin/inventory

**Các bước**:
1. Navigate đến Admin Portal → Branch Inventory
2. **BẮT BUỘC** hiển thị:
   - Branch selector dropdown
   - Product inventory table:
     - Product name
     - **Selling price** (per branch)
     - **Quantity on hand**
     - **Reserved quantity**
     - **Available quantity**
   - Edit/Adjust buttons
   - Stock replenishment form

---

### 📸 HÌNH 4.14: ORDER MANAGEMENT ⚠️ THIẾU
**URL**: http://localhost:5173/admin/orders

**Các bước**:
1. Navigate đến Admin Portal → Orders
2. **BẮT BUỘC** hiển thị:
   - Order filter (by status, branch, date)
   - Order list table
   - Status badges
   - "Update Status" buttons
   - Order detail modal với state transitions

---

### 📸 HÌNH 4.15: USER MANAGEMENT ⚠️ THIẾU
**URL**: http://localhost:5173/admin/users

**Các bước**:
1. Navigate đến Admin Portal → Users
2. **BẮT BUỘC** hiển thị:
   - User search/filter
   - User table với:
     - Email
     - Name
     - Role (Customer/Admin)
     - Lock status
   - "Lock Account" / "Unlock Account" buttons

---

### 📸 HÌNH 4.16a: ADMIN DASHBOARD (TỔNG QUAN HỆ THỐNG)
**URL**: http://localhost:5173/admin/dashboard

**Các bước**:
1. Đăng nhập quyền Admin (`admin@test.com` / `Test@123`)
2. Navigate đến Admin Portal → Tổng quan (`/admin/dashboard`)
3. **BẮT BUỘC** hiển thị:
   - Tiêu đề "Tổng quan hệ thống"
   - 4 thẻ KPI chỉ số:
     - 🧾 Tổng số đơn hàng (`totalOrders`)
     - ⏳ Đơn cần xử lý (`pendingOrders`)
     - 💰 Tổng doanh thu đơn hoàn tất (`completedRevenue`)
     - ⚠️ Hàng tồn thấp (`lowStockItems`)
   - Bảng danh sách đơn hàng gần đây: Mã đơn (click chuyển sang chi tiết đơn), Thời gian, Số món, Hình thức (Nhận tại kho / Giao hàng), Tổng tiền, Trạng thái đơn

---

### 📸 HÌNH 4.16b: ADMIN SALES REPORT (BÁO CÁO DOANH SỐ)
**URL**: http://localhost:5173/admin/reports/sales

**Các bước**:
1. Navigate đến Admin Portal → Báo cáo doanh số (`/admin/reports/sales`)
2. **BẮT BUỘC** hiển thị:
   - Chú thích "Nhóm theo ngày tạo đơn (UTC)"
   - Các nút preset chọn nhanh: 7 ngày, 30 ngày, Tháng này
   - Bộ chọn ngày tùy chỉnh: Từ ngày (`sales-from`), Đến ngày (`sales-to`) và nút "Xem báo cáo"
   - 3 thẻ KPI:
     - Tổng doanh thu
     - Số đơn hoàn tất
     - Giá trị đơn trung bình
   - Bảng thống kê chi tiết "Doanh thu theo ngày" (Ngày, Số đơn hoàn tất, Doanh thu)

---

### 📸 HÌNH 4.17: TRANG BÁO LỖI 404 NOT FOUND
**URL**: http://localhost:5173/not-found (hoặc bất kỳ đường dẫn không tồn tại nào)

**Các bước**:
1. Nhập một đường dẫn không tồn tại vào thanh địa chỉ trình duyệt
2. **BẮT BUỘC** hiển thị:
   - Mã lỗi nổi bật "404"
   - Tiêu đề "Không tìm thấy trang"
   - Đoạn văn mô tả hướng dẫn người dùng
   - Các nút điều hướng nhanh: "Về trang chủ" và "Xem sản phẩm"

---

## 5. CHỈNH SỬA ẢNH VÀ ANNOTATE

### 5.1. Thêm mũi tên và chỉ dẫn

**Sử dụng PowerPoint (có sẵn)**:
1. Insert → Screenshot
2. Chọn ảnh đã chụp
3. Insert → Shapes → Arrow
4. Vẽ mũi tên pointing đến key features
5. Insert → Text Box để thêm labels
6. Right-click shape → Format → Color/Style

**Sử dụng Paint 3D (Windows 10/11)**:
1. Mở ảnh trong Paint 3D
2. Tool bar → **Stickers** hoặc **Text**
3. Thêm labels như "Branch Selector", "Localized Price"

### 5.2. Thêm viền và Shadow

Trong PowerPoint:
1. Insert ảnh
2. Picture Format → Picture Border
3. Chọn màu (ví dụ: dark gray)
4. Picture Effects → Shadow → Outer

---

## 6. TỔ CHỨC FILE VÀ ĐẶT TÊN

### 6.1. Cấu trúc thư mục
```
II_eProject_Report/
├── images/
│   ├── Figure_4.1_Homepage_BranchSelector.png
│   ├── Figure_4.2_Login_Form.png
│   ├── Figure_4.2b_Reset_Password.png
│   ├── Figure_4.3_Authenticated_State.png
│   ├── Figure_4.4_Catalog_Filtering.png
│   ├── Figure_4.5_Product_Detail.png
│   ├── Figure_4.6_Product_Comparison.png
│   ├── Figure_4.7_Shopping_Cart.png
│   ├── Figure_4.8_Checkout_Delivery.png
│   ├── Figure_4.9_Payment_Gateway.png
│   ├── Figure_4.10_Order_History.png
│   ├── Figure_4.11_Admin_Categories.png
│   ├── Figure_4.12_Admin_Products.png
│   ├── Figure_4.13_Admin_Inventory.png
│   ├── Figure_4.14_Admin_Orders.png
│   ├── Figure_4.15_Admin_Users.png
│   ├── Figure_4.16a_Admin_Dashboard.png
│   ├── Figure_4.16b_Admin_Sales_Report.png
│   ├── Figure_4.17_Not_Found_404.png
│   ├── Diagram_Architecture.png
│   ├── Diagram_ERD.png
│   ├── Diagram_ContextDFD.png
│   └── Diagram_CheckoutFlow.png
```

### 6.2. Quy tắc đặt tên
- Format: `Figure_X.X_Description.png`
- Không dùng khoảng trắng, thay bằng `_`
- Mô tả ngắn gọn, không quá 30 ký tự

---

## 7. CHECKLIST TRƯỚC KHI NỘP

- [ ] Tất cả các hình (4.1-4.17) đã chụp
- [ ] Viewport: 1920x1080 (hoặc full page)
- [ ] Hiển thị đúng Branch đã chọn
- [ ] Thấy **localized prices** trên product cards
- [ ] Thấy **stock availability** per branch
- [ ] Đầy đủ UI elements (header, sidebar, footer)
- [ ] Đã annotate các key features
- [ ] Đặt tên file đúng format
- [ ] Lưu vào đúng thư mục `images/`
- [ ] Diagrams đã export sang PNG

---

## 8. HƯỚNG DẪN NHANH (Quick Reference)

| Hình | URL | Key Elements |
|:----:|:----|:------------|
| 4.1 | `/` | Homepage + Branch Selector |
| 4.2 | `/login` | Login form |
| 4.2b | `/reset-password` | Password reset form |
| 4.3 | Sau login | User badge, menu |
| 4.4 | `/products` | Filter sidebar + Product grid |
| 4.5 | `/products/:id` | Detail + Specs + Stock |
| 4.6 | Modal | Compare table |
| 4.7 | `/cart` | Cart items + Branch name |
| 4.8 | `/checkout` | Delivery + Payment options |
| 4.9 | Payment | Gateway redirect |
| 4.10 | `/account/orders` | Order list + Timeline |
| 4.11 | `/admin/categories` | Category tree |
| 4.12 | `/admin/products` | Product CRUD |
| 4.13 | `/admin/inventory` | Branch stock + prices |
| 4.14 | `/admin/orders` | Order management |
| 4.15 | `/admin/users` | User lock/unlock |
| 4.16a | `/admin/dashboard` | Dashboard metrics + Recent orders |
| 4.16b | `/admin/reports/sales` | Sales report UTC + Daily table |
| 4.17 | `/not-found` | 404 error page |

---

*Hanoi, September 2026*
