# AptechMart — Siêu thị điện tử đa chi nhánh

Ứng dụng bán hàng trực tuyến với giá bán và tồn kho riêng cho từng chi nhánh. Khách hàng tìm sản phẩm, đặt hàng, theo dõi đơn và đánh giá sản phẩm đã mua; quản trị viên quản lý danh mục, kho, đơn hàng, báo cáo doanh số, dự báo nhu cầu và gợi ý sản phẩm.

**Bắt đầu:** [Setup tiếng Việt](SETUP-VI.md) · [Hướng dẫn nghiệm thu](docs/testing/FINAL-UAT-2026-09-11.md) · [Kết quả test final](docs/testing/FINAL-RESULT-2026-09-11.md)

## Công nghệ

| Thành phần | Công nghệ |
|---|---|
| Backend | .NET 10, ASP.NET Core Minimal API |
| Database | MySQL 8.4, Entity Framework Core 10, MySql.EntityFrameworkCore |
| Frontend | React 19, TypeScript 5.9, Vite 7 |
| Xác thực | JWT, refresh token, phân quyền Customer/Admin |
| Gợi ý và dự báo | ML.NET, tác vụ nền, dự báo 7/14 ngày |
| Kiểm thử | xUnit, MySQL Testcontainers, Vitest, Playwright |
| Đóng gói | Docker Compose, Nginx |

## Chức năng

- **Khách hàng:** đăng ký/đăng nhập, hồ sơ và sổ địa chỉ; tìm kiếm/lọc sản phẩm; chọn chi nhánh; giỏ hàng; nhận tại cửa hàng hoặc giao tận nơi; lịch sử đơn; đánh giá sản phẩm theo điều kiện mua hàng.
- **Quản trị:** danh mục, thương hiệu, sản phẩm, chi nhánh; giá và tồn kho; nhật ký biến động kho; xử lý đơn; khóa/mở tài khoản; dashboard và báo cáo doanh số.
- **Tác vụ nền:** dự báo nhu cầu, gợi ý sản phẩm và giao diện quản lý tác vụ.
- **Thanh toán:** COD và MoMo/VNPay giả lập trong Development. Màn hình giả lập không thu tiền; đổi sang Sandbox vẫn chưa cấu hình cổng thật.
- **So sánh, coupon và khuyến mãi:** đã có mã nguồn/giao diện; tài liệu phạm vi release ghi deferred, không tính là nghiệm thu đầy đủ mọi biến thể.

## Chạy dự án

Chọn một trong hai cách dưới đây. Lần đầu cần Internet để tải dependencies hoặc Docker images.

### Cách 1: Visual Studio + MySQL + Vite

**Yêu cầu:** Visual Studio hỗ trợ .NET 10 và solution .slnx, workload ASP.NET; .NET SDK **10.0.400** hoặc bản vá tương thích theo [global.json](global.json); Node.js **24**; MySQL **8.4**.

1. Tạo database và tài khoản MySQL theo [hướng dẫn setup](SETUP-VI.md).
2. Mở solution **OnlineSupermarket.slnx** trong Visual Studio.
3. Sửa **ConnectionStrings.DefaultConnection** trong **backend/src/OnlineSupermarket.Api/appsettings.Development.json** theo MySQL đang dùng.
4. Chọn **OnlineSupermarket.Api** làm Startup Project, profile **http**, bấm **F5**. Kiểm tra [API health](http://localhost:5072/api/health).
5. Mở terminal tại thư mục dự án để chạy frontend:

~~~powershell
Set-Location frontend
npm.cmd ci
npm.cmd run dev
~~~

Mở **[http://localhost:5173](http://localhost:5173)**. Vite proxy /api về cổng **5072**. Giữ API và terminal frontend đang chạy.

Có thể chạy API bằng CLI thay Visual Studio, từ thư mục gốc:

~~~powershell
dotnet restore OnlineSupermarket.slnx
dotnet run --project backend/src/OnlineSupermarket.Api --launch-profile http
~~~

API Development tự migrate và seed dữ liệu; không cần import database-init.sql cho database mới. Tác vụ nền trong cấu hình Development mặc định bị tắt; đặt **Infrastructure.DisableBackgroundServices=false** nếu muốn chạy như Docker. Xem [SETUP-VI.md](SETUP-VI.md) để cấu hình database, chuỗi kết nối và xử lý lỗi.

### Cách 2: Docker toàn bộ hệ thống

**Yêu cầu:** Docker Desktop đang hoạt động với Linux containers. Không cần cài riêng .NET, Node.js hoặc MySQL trên host để chạy ứng dụng.

Tại thư mục gốc, chỉ copy cấu hình mẫu khi chưa có .env:

~~~powershell
Copy-Item .env.example .env
docker compose up --build -d
docker compose ps
Invoke-RestMethod http://localhost:8080/api/health
~~~

| Dịch vụ | Địa chỉ |
|---|---|
| Giao diện web | [http://localhost:5173](http://localhost:5173) |
| API health | [http://localhost:8080/api/health](http://localhost:8080/api/health) |
| OpenAPI Development | [http://localhost:8080/openapi/v1.json](http://localhost:8080/openapi/v1.json) |
| MySQL trên host | 127.0.0.1:3306 |

API tự migrate và tạo dữ liệu demo. Database/user/mật khẩu cấu hình trong .env; nếu đổi mật khẩu, cập nhật cả chuỗi kết nối. Dữ liệu được giữ trong Docker volume, không nằm trong repository.

`PAYMENTS_MODE=Mock` là mặc định cho demo: sau khi tạo đơn, chọn MoMo hoặc VNPay để mở trang **“Thanh toán giả lập — không thu tiền”** rồi chọn thành công, thất bại hoặc hủy. Sandbox hiện trả `PAYMENT_PROVIDER_NOT_CONFIGURED`; chưa có merchant credential hay giao dịch thật.

~~~powershell
# Xem lỗi khởi động
docker compose logs --tail 100 api mysql

# Sau khi sửa và lưu code, cập nhật image
docker compose up --build -d

# Dừng nhưng giữ dữ liệu
docker compose stop
~~~

Lệnh **docker compose up -d** chạy image hiện có; cần **--build** để cập nhật code. Không dùng **down -v** nếu muốn giữ database. Tránh chạy frontend Docker và Vite cùng cổng 5173. Hướng dẫn setup cũng có cách dùng MySQL Docker với API chạy bằng Visual Studio.

## Tài khoản demo

| Vai trò | Email | Mật khẩu seed |
|---|---|---|
| Admin | admin@test.com | Test@123 |
| Customer | user1@test.com | Test@123 |
| Customer | user2@test.com | Test@123 |
| Customer | user3@test.com | Test@123 |

Các tài khoản được tạo khi bảng người dùng trống. Database cũ có thể đã đổi mật khẩu. Đây là cấu hình local/demo; không dùng mật khẩu và khóa Development cho môi trường thật.

Sau đăng nhập, thử tìm **Samsung Galaxy S24 Ultra 256GB**, thêm giỏ và đặt COD; dùng cửa sổ admin riêng để xem đơn tại **/admin/orders**. ID đơn và tài khoản QA trong báo cáo final thuộc máy kiểm thử, không tự xuất hiện trên máy cài mới.

## Kiểm thử

Backend, từ thư mục gốc:

~~~powershell
dotnet test OnlineSupermarket.slnx
~~~

Cần Docker cho các test MySQL Testcontainers và Python 3 trong PATH cho một số test migration. Không cần Docker chỉ để chạy API native với MySQL đã cài.

Frontend:

~~~powershell
Set-Location frontend
npm.cmd ci
npm.cmd test -- --run
npm.cmd run build
~~~

UI E2E dùng API **8080** và frontend **5173**: bật stack Docker toàn bộ trước, sau đó chạy trong thư mục frontend:

~~~powershell
npx.cmd playwright install chromium
$env:UI_TEST_RUN_ID="FINAL-MYPC-01"
$env:UI_TEST_PREFIX="QA_MYPC_01_"
npx.cmd playwright test e2e/ui-full-plan
~~~

Đổi prefix mỗi đợt. Các ca UI sẽ tạo tài khoản, đơn và dữ liệu QA trong database ứng dụng.

### Kết quả đã ghi nhận ngày 11/09/2026

| Bộ kiểm tra | Kết quả |
|---|---|
| Backend Domain / API / Infrastructure | 171 + 294 + 246 = **711/711 đạt**, không bỏ qua |
| Frontend unit/component | **391/391 đạt** |
| UI | **32/32 ca có kết quả đạt qua nhiều lượt**: 26 ca + 6 hành trình chạy lại sau sửa test |
| Build | Docker API/frontend đạt; bản ZIP giải nén build backend Release và frontend thành công |

Đây là kết quả của đợt kiểm thử đã thực hiện, không phải trạng thái CI tự động. Bộ UI có giới hạn về assertion; số ca đạt không đồng nghĩa bao phủ mọi biến thể. Chưa xác minh đầy đủ cổng VNPay/MoMo bên ngoài, email reset thực tế và thao tác gửi đánh giá qua UI trong đợt này. Chi tiết và các lỗi test đã sửa nằm trong [báo cáo final](docs/testing/FINAL-RESULT-2026-09-11.md); các log/ảnh local không được commit cùng mã nguồn.

## Cấu trúc dự án

~~~text
backend/
  src/
    OnlineSupermarket.Api/             # API, cấu hình, Dockerfile
    OnlineSupermarket.Domain/          # Mô hình và quy tắc nghiệp vụ
    OnlineSupermarket.Infrastructure/  # EF, migrations, seed, services
  tests/                              # Domain, API, MySQL integration tests
frontend/
  src/                                # React, API client, giao diện khách/admin
  e2e/ui-full-plan/                    # Playwright
docs/                                 # Yêu cầu, kiến trúc, OpenAPI, nghiệm thu
compose.yaml                          # MySQL + API + frontend
OnlineSupermarket.slnx                 # Solution .NET
SETUP-VI.md                           # Setup và xử lý lỗi thường gặp
~~~

## Tài liệu

- [Setup Visual Studio và Docker](SETUP-VI.md)
- [Hướng dẫn UAT với dữ liệu demo](docs/testing/FINAL-UAT-2026-09-11.md)
- [Báo cáo final và giới hạn kiểm thử](docs/testing/FINAL-RESULT-2026-09-11.md)
- [Yêu cầu chức năng](docs/requirements/functional-requirements.md)
- [Thiết kế database / ERD](docs/architecture/erd.md)
- [Luồng dữ liệu / DFD](docs/architecture/dfd.md)
- [Sitemap](docs/architecture/sitemap.md)
- [OpenAPI contract](docs/api/openapi.json)
