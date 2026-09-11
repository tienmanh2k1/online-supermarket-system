# Kết quả test final sau khi bật Docker — 11/09/2026

## Kết quả xác minh

Docker đã được build lại bằng `docker compose up --build -d` từ `C:\Users\manh\Documents\Project3\online-supermarket-system`. API trả `{"status":"ok"}`. Image phản ánh mã nguồn đã lưu trong thư mục này tại thời điểm build; không xác nhận những thay đổi chưa lưu trong Visual Studio. Sau build chỉ sửa các file kiểm thử, không sửa mã nghiệp vụ.

| Bộ kiểm tra | Kết quả |
|---|---|
| Backend Domain | 171/171 đạt |
| Backend API | 294/294 đạt |
| Backend Infrastructure, gồm MySQL Testcontainers | 246/246 đạt |
| Tổng backend | **711/711 đạt, 0 bỏ qua**, lệnh trả exit code 0 |
| Frontend unit/component (lượt trước cùng phiên) | 391/391 đạt; không chạy lại vì không sửa mã frontend nghiệp vụ |
| Frontend build (lượt trước) | Đạt, còn cảnh báo bundle lớn |
| Docker build API và frontend (lượt mới) | Đạt, các container khởi chạy |
| UI lần đầu | 27 đạt, 1 lỗi chuẩn bị dữ liệu, 4 không chạy trong tổng 32 ca |
| UI nhóm 6 hành trình sau chuẩn bị dữ liệu | 6/6 đạt, nhưng đối chiếu độc lập phát hiện một thông báo PASS sai về trạng thái đơn |
| UI nhóm 6 hành trình sau sửa kiểm tra trạng thái, dùng prefix mới | **6/6 đạt**, exit code 0; xác minh thêm đơn Delivery đã Completed qua API |

Tổng hợp theo ca duy nhất: **26 ca UI ngoài nhóm hành trình đạt ở lượt đầu + 6 ca hành trình đạt ở lượt cuối = 32/32 ca có kết quả đạt**. Đây là kết quả hợp nhất nhiều lượt, không phải một lần chạy toàn bộ 32 ca sau chỉnh sửa.

## Những sửa đổi cần thiết trong bộ test

1. `CatalogEndpointsTests.cs`: hai assertion đọc `Meta.TotalCount` theo DTO thực tế, khắc phục lỗi biên dịch. Toàn bộ 294 API test đã chạy đạt sau sửa.
2. `fixtures.ts`, `reporter-helper.ts`: cho phép đặt prefix và mã đợt bằng biến môi trường để giữ nguyên bằng chứng cũ.
3. `e2e-journeys.spec.ts`: tự chuẩn bị C1 và C3; trước đây file được chạy trước phase1 nên C1 chưa tồn tại. Đổi chuỗi trạng thái đơn thành `Preparing → Ready → Delivered → Completed`, bắt buộc kiểm tra mỗi trạng thái sau submit. Bản cũ dùng `Processing`, bỏ qua option không tồn tại và ghi Completed dù API vẫn trả Confirmed.

Không sửa chức năng ứng dụng để làm test đạt. `git diff --check` không báo lỗi whitespace.

## Giới hạn của kết luận

- Bộ UI hiện có nhiều kiểm tra có điều kiện (`if visible`) và các tên ca bao trùm nhiều thao tác hơn assertion thực tế. **32 ca đạt không đồng nghĩa mọi biến thể của mọi chức năng đều được xác minh.** Không dùng số record PASS tự ghi trong ledger để tuyên bố bao phủ 100%.
- Ca UI VNPay chỉ kiểm tra lựa chọn phương thức, chưa xác minh cổng ngân hàng, redirect và callback xuyên suốt. Backend test thanh toán đã nằm trong bộ 711 test, nhưng không thay thế kiểm thử cổng bên ngoài. MoMo end-to-end và email reset thực tế chưa được xác minh trong lượt này.
- Ca E2E-01 đã xác minh hoàn tất đơn, chưa tự gửi đánh giá dù tên ca có chữ Review. Bộ backend có test đánh giá; kiểm thử đăng đánh giá qua UI vẫn cần ca riêng nếu nghiệm thu đầy đủ.
- Các ca forecast/recommendations chủ yếu kiểm tra giao diện/trigger; không chứng minh chất lượng dự báo bằng dữ liệu bán hàng thực.
- Bằng chứng trước khi sửa E2E-01 ghi Completed cho đơn `b11667dd-3191-4696-8167-dc67a084607d` là sai. Snapshot API cho thấy Confirmed. Giữ log gốc để truy vết; dùng đơn đã xác minh dưới đây cho demo hoàn tất.

**Kết luận:** các bộ tự động đã chạy đạt trong phạm vi assertion nêu trên; đã gỡ các điểm chặn môi trường/biên dịch. Chưa tuyên bố nghiệm thu 100% các luồng bên ngoài hoặc mọi biến thể UI.

## Dữ liệu đang có thật trong database Docker

Đây là dữ liệu demo được đọc trực tiếp từ API ngày 11/09, không phải khách hàng/giao dịch thương mại thật. Tồn kho có thể thay đổi sau thao tác tiếp theo.

### Tài khoản

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Admin đã đăng nhập thành công | admin@test.com | Test@123 |
| Khách QA đợt 02 đã tạo và dùng qua UI | qa_final_20260911_02_c1@test.com | Password@123 |
| Khách QA đợt 03 đã tạo và dùng qua UI | qa_final_20260911_03_c1@test.com | Password@123 |

Mở http://localhost:5173; dùng một cửa sổ admin, một cửa sổ khách hàng. Các prefix `QA_FINAL_20260911_02_` và `QA_FINAL_20260911_03_` giúp nhận diện dữ liệu tạo trong lượt test. Dữ liệu QA được giữ lại để bạn xem; có phát sinh đơn, giữ chỗ/tồn kho, danh mục, thương hiệu, sản phẩm và khuyến mãi demo từ các ca hiện có. Không xóa volume/database.

### Sản phẩm và giá bán lấy từ API

| SKU | Tên | Giá bán (đ), cả 3 chi nhánh lúc chụp | Khả dụng Bình Thạnh / Quận 1 / Quận 3 |
|---|---|---:|---|
| DT-SAM-001 | Samsung Galaxy S24 Ultra 256GB | 28.990.000 | 40 / 30 / 25 |
| DT-APP-001 | iPhone 15 Pro Max 256GB | 34.990.000 | 40 / 30 / 25 |
| AT-JBL-001 | JBL PartyBox 310 | 14.990.000 | 40 / 30 / 25 |

API danh sách có 36 sản phẩm trước khi bộ UI tạo thêm sản phẩm QA. Snapshot giá/tồn cùng ID đầy đủ: `artifacts/final-resume-20260911/live-test-data.json`. Snapshot danh mục: `live-products.json`; chi nhánh: `live-branches.json` cùng thư mục.

### Hai đơn có thể mở để đối chiếu

| Đơn | Nội dung | Tiền và trạng thái đã xác minh qua API |
|---|---|---|
| `6a515cdb-2acc-4945-95ae-b1c9980efa79` | 2 AirPods Pro 2, Pickup, COD; khách QA đợt 02 C1 | 11.980.000 + phí 0 = **11.980.000đ**, **Completed** |
| `2c0f448d-bf5c-4548-9262-f436a5bde044` | 1 AirPods Pro 2, Delivery, COD; khách E2E tự tạo | 5.990.000 + phí 15.000 = **6.005.000đ**, **Completed** |

Đăng nhập admin rồi mở:

- http://localhost:5173/admin/orders/6a515cdb-2acc-4945-95ae-b1c9980efa79
- http://localhost:5173/admin/orders/2c0f448d-bf5c-4548-9262-f436a5bde044

Với khách `qa_final_20260911_02_c1@test.com`, mở `/orders/history` để xem đơn Pickup thuộc tài khoản này. Đơn Delivery thuộc tài khoản E2E riêng, xem qua admin. Snapshot đối chiếu: `live-created-orders.json` và `live-verified-delivery-order.json`.

Để tự test tiếp, dùng 32 kịch bản trong [hướng dẫn UAT](FINAL-UAT-2026-09-11.md), lấy giá/tồn hiện tại trước mỗi lần đặt hàng. Không dùng số tồn snapshot như hằng số sau nhiều lượt mua.

## Bằng chứng

- `artifacts/final-resume-dotnet-20260911.log` và `artifacts/final-resume-20260911/*.trx`: 711 backend tests.
- `artifacts/final-resume-docker-build-20260911.log`: build và khởi chạy image.
- `artifacts/final-resume-playwright-20260911.log`: 32 ca lượt đầu, gồm lỗi/ca chưa chạy.
- `artifacts/final-resume-journeys-rerun-20260911.log`: chạy lại sau chuẩn bị dữ liệu, chưa sửa assertion trạng thái.
- `artifacts/final-resume-journeys-verified-20260911.log`: 6 ca sau sửa, prefix `QA_FINAL_20260911_03_`.
- `artifacts/ui-full-test/FINAL-20260911-02/`, `artifacts/ui-full-test/FINAL-20260911-03/`: ledger/ảnh mới; cần diễn giải theo giới hạn ở trên.
- `artifacts/final-resume-20260911/playwright/`: ảnh và error context lượt đầu.

## Lệnh chạy lại

Từ thư mục dự án, khi Docker engine đang hoạt động:

```powershell
docker compose up --build -d
dotnet test OnlineSupermarket.slnx --no-restore
Set-Location frontend
$env:UI_TEST_RUN_ID="FINAL-YYYYMMDD-04"
$env:UI_TEST_PREFIX="QA_FINAL_YYYYMMDD_04_"
npx.cmd playwright test e2e/ui-full-plan
```

Thay prefix bằng đợt mới. Lệnh UI sẽ ghi thêm dữ liệu QA. Backend MySQL tests tự dùng database/container test riêng. Không cần chạy lại frontend unit/build chỉ vì đổi nội dung tài liệu.
