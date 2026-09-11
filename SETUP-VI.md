# Setup AptechMart — bản bàn giao 11/09/2026

## Bắt đầu

Giải nén ZIP vào thư mục ngắn, ví dụ `C:\Projects\AptechMart`. Thư mục dự án là nơi chứa `OnlineSupermarket.slnx`, `compose.yaml` và tài liệu này. Mở PowerShell tại đó. Chọn **một** trong hai cách dưới đây.

Đây là gói mã nguồn dành cho học tập/demo local. Cần Internet lần đầu để tải NuGet, npm hoặc Docker images. ZIP không chứa SDK, node_modules, image Docker hay bản sao database hiện tại. Cấu hình và mật khẩu mẫu chỉ dùng local.

## Cách 1 — Visual Studio + MySQL + frontend Vite

### Yêu cầu

- Visual Studio có hỗ trợ .NET 10 và định dạng solution `.slnx`; workload **ASP.NET and web development**.
- .NET SDK **10.0.400** hoặc bản vá tương thích cùng feature band theo `global.json`. Kiểm tra bằng `dotnet --version` ngay trong thư mục dự án. SDK 8/9 không đáp ứng dự án này.
- Node.js **24**, npm đi kèm; kiểm tra `node --version` và `npm.cmd --version`.
- MySQL **8.4**, đang lắng nghe cổng 3306.

### 1. Tạo database local

Trong MySQL Workbench hoặc MySQL CLI, đăng nhập bằng tài khoản quản trị rồi chạy trên một database mới:

```sql
CREATE DATABASE IF NOT EXISTS online_supermarket CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'supermarket_app'@'%' IDENTIFIED BY 'change_me';
GRANT ALL PRIVILEGES ON online_supermarket.* TO 'supermarket_app'@'%';
```

Nếu user đã tồn tại, lệnh trên không đổi mật khẩu: dùng mật khẩu hiện có trong cấu hình bước tiếp theo. Database/user mẫu này chỉ phục vụ local.

Nếu muốn dùng Docker chỉ cho MySQL thay vì cài MySQL native:

```powershell
Copy-Item .env.example .env
docker compose up -d mysql
```

Thực hiện Copy-Item chỉ khi chưa có `.env`. MySQL container dùng database `online_supermarket`, user `supermarket_app`, mật khẩu `change_me` theo file mẫu. Chờ MySQL healthy trước khi chạy Visual Studio. Không chạy MySQL native cùng cổng 3306.

### 2. Cấu hình và chạy API trong Visual Studio

1. Mở `OnlineSupermarket.slnx`.
2. Mở `backend/src/OnlineSupermarket.Api/appsettings.Development.json`.
3. Đặt `ConnectionStrings.DefaultConnection` theo MySQL của bạn, ví dụ:

```json
"DefaultConnection": "Server=127.0.0.1;Port=3306;Database=online_supermarket;User=supermarket_app;Password=change_me;CharSet=utf8mb4;UseAffectedRows=False;"
```

4. Muốn chạy tác vụ nền dự báo/gợi ý giống Docker, đặt `Infrastructure.DisableBackgroundServices` thành `false`. Bản cấu hình Development mặc định là `true`.
5. Đặt **OnlineSupermarket.Api** làm Startup Project, chọn profile **http**, bấm **F5** hoặc **Ctrl+F5**.
6. Mở `http://localhost:5072/api/health`, phải nhận `{"status":"ok"}`. API không tự mở cửa sổ trình duyệt vì profile đặt `launchBrowser=false`.

Trong Development, API tự chạy EF migrations và seed dữ liệu ban đầu. **Không cần import `database-init.sql` vào database mới theo luồng này**; tránh trộn SQL cũ với migrations hiện tại. Lần đầu có thể chờ lâu hơn. Nếu API dừng, xem Output của Visual Studio để biết lỗi database.

Cách chạy API tương đương bằng terminal:

```powershell
dotnet restore OnlineSupermarket.slnx
dotnet run --project backend/src/OnlineSupermarket.Api --launch-profile http
```

### 3. Chạy frontend

Mở terminal thứ hai tại thư mục dự án:

```powershell
Set-Location frontend
npm.cmd ci
npm.cmd run dev
```

Mở **http://localhost:5173**. Vite proxy `/api` sang **http://localhost:5072** theo `frontend/vite.config.ts`. Giữ terminal frontend và API đang chạy trong lúc sử dụng.

Nếu trước đó đã bật Docker toàn bộ, dùng `docker compose stop api frontend` tại thư mục gốc để nhường cổng và tránh truy cập nhầm phiên bản; có thể giữ MySQL Docker. Khi chạy Visual Studio, không đặt frontend trỏ vào API Docker 8080 nếu muốn test đúng bản Visual Studio.

## Cách 2 — Docker toàn bộ hệ thống

Chỉ cần Docker Desktop đang hoạt động với Linux containers/Docker Compose; không cần cài Node/.NET/MySQL trên host cho ứng dụng.

Trong PowerShell tại thư mục dự án, khi chưa có `.env`:

```powershell
Copy-Item .env.example .env
docker compose up --build -d
docker compose ps
Invoke-RestMethod http://localhost:8080/api/health
```

- Web: **http://localhost:5173**.
- API health: **http://localhost:8080/api/health**.
- OpenAPI Development: **http://localhost:8080/openapi/v1.json**.
- MySQL host: **127.0.0.1:3306**; database/user/mật khẩu trong `.env`.

File `.env.example` đã khai báo đủ `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD`, `MYSQL_ROOT_PASSWORD`, chuỗi kết nối và `VITE_API_BASE_URL`. Nếu đổi mật khẩu database, cập nhật đồng thời chuỗi kết nối. Các biến khởi tạo MySQL chỉ áp dụng khi volume trống; đổi `.env` không tự đổi mật khẩu trong volume cũ.

`PAYMENTS_MODE=Mock` là cấu hình demo mặc định. Khi checkout, chọn MoMo hoặc VNPay để vào trang **Thanh toán giả lập — không thu tiền**; ba nút chỉ mô phỏng Success, Failed và Cancelled trong database local. Không dùng thông tin merchant, không tạo QR/OTP và không thu tiền. `Sandbox` hiện chỉ dùng để tắt thanh toán online với mã `PAYMENT_PROVIDER_NOT_CONFIGURED`, không phải bật thanh toán thật.

API Docker chạy Development và tự migrate/seed. Không cần import SQL thủ công. Dữ liệu được lưu trong named volume `mysql_data`. Gói này không chứa các đơn QA từ máy tác giả; máy mới có dữ liệu seed, ID được tạo mới.

Các lệnh thường dùng:

```powershell
# Xem lỗi khởi động
docker compose logs --tail 100 api mysql

# Sau khi sửa và lưu code: build lại image
docker compose up --build -d

# Dừng, giữ dữ liệu
docker compose stop

# Chạy lại image hiện có
docker compose up -d
```

Không dùng `docker compose down -v` nếu muốn giữ dữ liệu: tùy chọn `-v` xóa volume database.

## Đăng nhập và kiểm tra sau setup

| Vai trò | Email seed | Mật khẩu seed |
|---|---|---|
| Admin | admin@test.com | Test@123 |
| Khách hàng | user1@test.com | Test@123 |
| Khách hàng | user2@test.com | Test@123 |
| Khách hàng | user3@test.com | Test@123 |

Seeder chỉ tạo user khi bảng người dùng đang trống. Database cũ có thể có tài khoản/mật khẩu khác.

Kiểm tra nhanh:

1. Trang chủ và `/browse` hiển thị sản phẩm.
2. Tìm `Samsung Galaxy S24 Ultra 256GB`, đổi chi nhánh và xem tồn.
3. Đăng nhập user1, thêm sản phẩm vào giỏ, thử COD nhận tại cửa hàng.
4. Đăng nhập admin bằng cửa sổ ẩn danh, vào `/admin/orders` xem đơn vừa tạo.
5. Mở `/admin/inventory` và `/admin/reports/sales` kiểm tra dữ liệu tải được.

Hướng dẫn kiểm thử đầy đủ: `docs/testing/FINAL-UAT-2026-09-11.md`. Kết quả đã chạy tại máy bàn giao và giới hạn kiểm thử: `docs/testing/FINAL-RESULT-2026-09-11.md`. Các UUID/tài khoản QA trong báo cáo là snapshot máy bàn giao, **không đảm bảo có trên máy cài mới**; dùng tài khoản seed và tìm sản phẩm theo tên/SKU.

## Chạy test

Backend tại thư mục gốc, cần .NET SDK và Docker engine cho MySQL Testcontainers; một số test migration gọi Python nên cần Python 3 trong PATH:

```powershell
dotnet test OnlineSupermarket.slnx
```

Frontend:

```powershell
Set-Location frontend
npm.cmd ci
npm.cmd test -- --run
npm.cmd run build
```

UI E2E: bộ hiện có dùng API 8080 và frontend 5173, nên bật **Docker toàn bộ** trước. Sau `npm ci`, cài browser một lần:

```powershell
npx.cmd playwright install chromium
$env:UI_TEST_RUN_ID="FINAL-MYPC-01"
$env:UI_TEST_PREFIX="QA_MYPC_01_"
npx.cmd playwright test e2e/ui-full-plan
```

Đổi prefix mỗi đợt. UI test tạo tài khoản, đơn và dữ liệu QA trong database ứng dụng. Không chạy vào database bán hàng thực.

## Lỗi thường gặp

| Hiện tượng | Cách kiểm tra |
|---|---|
| Không tìm thấy SDK 10.0.400 | Chạy `dotnet --list-sdks`; cài SDK phù hợp `global.json`, không chỉ runtime |
| Visual Studio không mở được `.slnx`/net10.0 | Dùng bản Visual Studio hỗ trợ cả hai hoặc chạy CLI như hướng dẫn |
| API connection refused | Kiểm tra API đã chạy; native dùng 5072, Docker dùng 8080 |
| MySQL Access denied | Kiểm tra database/user/mật khẩu thực tế, cả appsettings và biến môi trường đang ghi đè cấu hình |
| Cổng 3306/5173/8080 bị chiếm | Dừng tiến trình/container đang dùng cổng hoặc cấu hình cổng đồng bộ; tránh chạy hai stack cùng lúc |
| Web mở được nhưng API lỗi | Native: kiểm tra Vite proxy 5072; Docker: xem log API và MySQL |
| Docker báo thiếu MYSQL_USER/MYSQL_PASSWORD | Tạo `.env` từ `.env.example` và kiểm tra đủ biến; không copy đè cấu hình cũ tùy tiện |
| Sai mật khẩu seed | User đã có trong database; seed không đặt lại mật khẩu user hiện hữu |
| Ảnh ngoài mạng không tải | Một số ảnh dùng URL bên ngoài; cần Internet |
| Chờ email reset/cổng thanh toán thật | Bản local dùng cấu hình email Development và thanh toán sandbox; xem giới hạn ở báo cáo final |

## Thành phần gói

Mã backend/frontend và tests, lockfile npm, Dockerfiles/Compose, cấu hình mẫu, SQL và tài liệu liên quan, báo cáo final và bằng chứng chọn lọc. File `PACKAGE-MANIFEST.sha256` liệt kê SHA-256 của từng file đóng gói. Không kèm `.env` cá nhân, `.git`, cache, `bin/obj`, `node_modules`, thư mục bản đóng gói cũ hoặc dữ liệu volume Docker.
