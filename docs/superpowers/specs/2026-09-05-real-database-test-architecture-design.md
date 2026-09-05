# Thiết kế kiến trúc kiểm thử với MySQL thật

## 1. Mục tiêu

Chuyển các kiểm thử cần xác minh hành vi persistence, transaction, migration và luồng API sang MySQL 8.4 thật mà vẫn bảo đảm kết quả lặp lại được, không phụ thuộc database Development và không có khả năng ghi hoặc xóa dữ liệu Production.

Kiến trúc đích gồm bốn tầng:

1. Unit test tiếp tục dùng mock hoặc provider nhẹ khi không cần đặc tính MySQL.
2. Integration và API test dùng MySQL 8.4 Testcontainers, khởi tạo database tạm cho từng test run.
3. Full E2E chạy trên môi trường Staging cô lập với dữ liệu production-like cố định hoặc snapshot Production đã ẩn danh.
4. Sau khi deploy Production chỉ chạy smoke test không phá hủy dữ liệu.

## 2. Phạm vi

### Trong phạm vi

- Chuẩn hóa một MySQL Testcontainer dùng chung cho các project test backend.
- Chuyển API test từ EF Core InMemory sang MySQL thật.
- Chuyển các integration test đang dùng MySQL local hardcode sang Testcontainers.
- Giữ unit test thuần domain và các test không kiểm tra SQL ở provider hiện tại.
- Tạo bộ seed kiểm thử xác định, dùng khóa ổn định và có thể gọi lại nhiều lần.
- Tạo full-stack E2E profile gồm frontend, API và MySQL test/staging.
- Sửa GUI checkout test để dùng seed chính thức thay vì ID và tài khoản thủ công.
- Phân tách rõ lệnh chạy unit, integration, E2E và production smoke.
- Bổ sung các chốt an toàn cho connection string và thao tác phá hủy database.
- Bổ sung quy trình CI, staging, migration và kiểm tra sau deploy.

### Ngoài phạm vi

- Chạy integration hoặc E2E test trên database Development đang được dùng chung.
- Chạy test tạo/sửa/xóa dữ liệu nghiệp vụ trên Production.
- Sao chép dữ liệu Production chưa ẩn danh vào local hoặc CI.
- Tích hợp cổng thanh toán thật trong test.
- Thay đổi nghiệp vụ của catalog, cart, checkout, inventory, recommendation hoặc forecast ngoài những gì cần để test lặp lại được.

## 3. Nguyên tắc an toàn bắt buộc

- Mọi process integration/API test phải chạy với `ASPNETCORE_ENVIRONMENT=Testing`.
- Test chỉ được dùng connection string do fixture cấp; không đọc `ConnectionStrings__DefaultConnection` từ `.env` Development.
- Tên database có thể bị tạo, reset hoặc drop phải bắt đầu bằng `test_` hoặc kết thúc bằng `_tests`.
- Database guard phải từ chối các tên `OnlineSupermarket`, `production`, `prod`, database trống và database hệ thống MySQL.
- Database guard phải từ chối hostname Production được cấu hình trong danh sách cấm của CI/CD.
- Không hardcode mật khẩu MySQL trong source test.
- Không test nào được drop database qua kết nối root tới MySQL local dùng chung.
- Production smoke test không gọi migration, seed, truncate, delete hoặc endpoint làm thay đổi đơn hàng/tồn kho.
- Snapshot Production chỉ được đưa vào Staging sau khi ẩn danh và phải được lưu trong kho artifact có kiểm soát truy cập, không commit vào Git.

Nếu một chốt an toàn thất bại, test phải dừng trước khi mở kết nối có quyền ghi.

## 4. Phân loại test đích

### 4.1 Unit tests

Các test domain và thuật toán không phụ thuộc SQL tiếp tục chạy không cần Docker. Ví dụ: entity invariants, password hashing, promotion calculation, forecast calculation và recommendation scoring.

EF Core InMemory chỉ được giữ cho test metadata/model thuần túy không nhằm xác minh constraint hoặc transaction. SQLite chỉ được giữ khi test chủ ý kiểm tra logic relational chung, không khẳng định hành vi riêng của MySQL.

### 4.2 Integration tests với MySQL

Các test sau bắt buộc dùng MySQL 8.4 Testcontainers:

- Migration và schema.
- Unique index, foreign key, check constraint và collation.
- Transaction, rollback, isolation và concurrency.
- Query translation và hành vi provider-specific.
- Job lease, inventory mutation, product view store và các persistence store khác.

Một collection fixture cấp container và connection string. Schema được tạo bằng migrations thật. Mỗi test class hoặc test case nhận database/schema cô lập theo chiến lược được fixture cung cấp, rồi dọn dữ liệu bằng cách drop database tạm hoặc reset có kiểm soát.

### 4.3 API tests với MySQL

`WebApplicationFactory<Program>` không được gỡ EF MySQL để thay bằng InMemory. Factory nhận connection string từ fixture, đặt environment `Testing`, tắt background services và thay email sender bằng test double. Migrations và seed test được chạy rõ ràng trong fixture trước khi tạo client.

API tests tiếp tục gọi HTTP in-process, nhưng toàn bộ thao tác persistence đi qua MySQL thật. Mỗi test phải tự tạo dữ liệu cần thiết qua seed builder hoặc API setup và không phụ thuộc thứ tự test.

### 4.4 Full E2E trên Staging

E2E chạy frontend và API thật, không intercept các route `/api`. Payment provider bên ngoài vẫn dùng sandbox hoặc stub tại boundary của provider.

Staging có database riêng, được tạo từ migrations hiện tại và một trong hai nguồn dữ liệu:

- Mặc định: seed production-like có version trong source, nhỏ và xác định.
- Tùy chọn: snapshot Production đã ẩn danh cho kiểm thử quy mô hoặc phân bố dữ liệu.

GUI checkout phải lấy product bằng SKU ổn định, đăng nhập bằng tài khoản E2E chính thức và chuẩn bị cart qua API. Không hardcode GUID sinh ngẫu nhiên từ seed runtime.

### 4.5 Production smoke tests

Sau deploy chỉ kiểm tra:

- Health/liveness và readiness.
- Kết nối database và migration version kỳ vọng.
- Các endpoint đọc công khai như branches và catalog.
- Đăng nhập tài khoản canary nếu được vận hành cấp riêng.

Smoke test mặc định là read-only. Nếu sau này cần write canary, dữ liệu phải có namespace riêng, endpoint cleanup riêng và feature flag Production; nội dung này cần một thiết kế độc lập trước khi triển khai.

## 5. Thành phần và trách nhiệm

### `MySqlTestContainerFixture`

- Khởi động image `mysql:8.4` một lần cho test collection.
- Cấp connection string không chứa database Production/Development.
- Tạo database test có tên ngẫu nhiên hợp lệ.
- Chạy EF Core migrations.
- Dọn container khi collection kết thúc.

### `TestDatabaseGuard`

- Parse connection string bằng `MySqlConnectionStringBuilder`.
- Xác minh environment là `Testing`.
- Xác minh tên database và hostname theo allowlist/denylist.
- Ném `InvalidOperationException` trước mọi thao tác reset/drop nếu không an toàn.
- Có unit test riêng cho các connection string được phép và bị từ chối.

### `TestDataSeeder`

- Chỉ chứa dữ liệu dùng cho automated test.
- Dùng GUID hoặc natural key cố định cho branch, user, product và promotion cốt lõi.
- Cung cấp tài khoản admin, customer và checkout E2E với password test lấy từ cấu hình test.
- Có thể chạy lại mà không nhân đôi dữ liệu.
- Tách khỏi `DataSeeder` Development để thay đổi demo data không làm hỏng test.

### `MySqlApiFactory`

- Kế thừa `WebApplicationFactory<Program>`.
- Nhận fixture và connection string qua constructor.
- Cấu hình `Testing`, tắt hosted services và dùng email sender bộ nhớ.
- Không thay MySQL bằng InMemory.
- Không chỉnh biến môi trường process-global rồi khôi phục thủ công.

### E2E runner

- Nhận `E2E_BASE_URL`, `E2E_API_URL`, credentials và SKU từ environment.
- Thực hiện readiness check trước khi chạy.
- Chuẩn bị dữ liệu qua API hoặc seed job dành cho Staging.
- Không mock route nội bộ `/api` trong full E2E profile.
- Lưu screenshot, trace và log vào thư mục artifact tương đối với workspace/CI.

### Production smoke runner

- Nhận URL Production từ secret/environment CI.
- Chỉ gọi danh sách endpoint cho phép.
- Không nhận database credentials.
- Trả exit code khác 0 khi health, schema version hoặc endpoint đọc thất bại.

## 6. Luồng dữ liệu

### Integration/API test

```text
test runner
  -> khởi động MySQL 8.4 Testcontainer
  -> TestDatabaseGuard kiểm tra connection string
  -> EF Core áp dụng migrations
  -> TestDataSeeder nạp baseline
  -> test gọi service hoặc HTTP API
  -> assertion đọc lại từ MySQL
  -> container bị hủy
```

### Staging E2E

```text
build artifacts
  -> deploy API + frontend + MySQL staging
  -> áp dụng migrations
  -> nạp seed production-like hoặc snapshot đã ẩn danh
  -> readiness check
  -> Playwright gọi UI và API thật
  -> thu artifacts
  -> reset môi trường staging
```

### Production

```text
backup/restore verification
  -> migration job có kiểm soát
  -> deploy API/frontend
  -> readiness check
  -> read-only smoke tests
  -> theo dõi metrics/logs
  -> rollback nếu gate thất bại
```

## 7. Chiến lược dữ liệu

Baseline production-like tối thiểu phải có:

- 2 chi nhánh hoạt động.
- 2 category cha và ít nhất 4 category lá.
- 3 brand và 8 sản phẩm với SKU cố định.
- Inventory cho từng chi nhánh, gồm đủ hàng, sắp hết và hết hàng.
- Customer thường, customer đủ điều kiện review và admin.
- Địa chỉ giao hàng mặc định/không mặc định.
- Cart rỗng và cart có hàng.
- Đơn hàng ở các trạng thái Pending, Paid, Completed và Cancelled.
- Promotion hợp lệ, hết hạn, chưa đủ minimum và hết lượt.
- Product view events, recommendation results và demand forecast đủ để kiểm tra các trang intelligence.

Dữ liệu phải được tạo qua domain constructors hoặc public setup helpers, không chèn SQL tùy tiện trừ dữ liệu chuyên dùng kiểm tra schema. Test thay đổi tồn kho, order hoặc promotion phải tạo bản ghi riêng theo test ID để tránh phụ thuộc baseline.

## 8. Migration và schema

- Testcontainer luôn bắt đầu từ database trống và chạy toàn bộ migrations theo thứ tự.
- Schema test xác minh bảng/index/constraint bằng `information_schema` trên container.
- Không dùng `EnsureCreated` cho test xác minh production schema.
- Pipeline phải có một test nâng cấp từ snapshot schema của release gần nhất lên migration hiện tại.
- Migration Production chạy bằng job riêng có quyền tối thiểu; API Production không tự migrate khi khởi động.
- Trước migration Production phải có backup và một lần restore verification gần đây.

## 9. CI/CD và lệnh chạy

Các nhóm lệnh cần được tách rõ:

- `test:unit`: không cần Docker, chạy ở mọi pull request.
- `test:integration`: cần Docker/Testcontainers, chạy ở mọi pull request.
- `test:e2e`: deploy full stack test hoặc Staging rồi chạy Playwright, chạy trước merge/release.
- `test:smoke:production`: read-only, chỉ chạy sau deploy Production.

Pipeline không truyền `.env` Development vào test. Credentials Testcontainers do library sinh; staging/production secrets lấy từ secret store của CI.

Release bị chặn nếu unit, integration hoặc staging E2E thất bại. Production smoke thất bại sẽ dừng rollout hoặc kích hoạt rollback theo nền tảng deploy.

## 10. Xử lý lỗi và chẩn đoán

- Container không khởi động: báo rõ Docker/Testcontainers unavailable, không fallback sang InMemory.
- Migration lỗi: giữ container/log artifact đủ lâu trong CI để chẩn đoán, sau đó cleanup.
- Seed lỗi: dừng test run trước khi test cases bắt đầu.
- E2E lỗi: lưu Playwright trace, screenshot, browser console và API logs.
- Production smoke lỗi: không thử ghi dữ liệu để xác minh; báo endpoint, status code và correlation ID đã được lọc bí mật.
- Log không được chứa password, JWT, refresh token hoặc raw PII từ snapshot.

## 11. Lộ trình chuyển đổi

1. Xây guard, fixture và seed baseline dùng chung.
2. Chuyển các MySQL test hardcode local sang fixture.
3. Chuyển API factory và API endpoint tests sang MySQL.
4. Phân loại và giữ lại unit tests phù hợp ở InMemory/SQLite.
5. Chuẩn hóa E2E runner và sửa checkout/intelligence flows để dùng API thật.
6. Tạo compose/profile Staging và pipeline gates.
7. Thêm production smoke runner read-only cùng deployment gate.
8. Loại bỏ connection string test hardcode, GUID E2E thủ công và script artifact path gắn với máy cá nhân.

Mỗi bước phải giữ test suite chạy được; không thực hiện chuyển đổi toàn bộ trong một commit.

## 12. Tiêu chí nghiệm thu

- Không còn API endpoint test nào dùng EF Core InMemory.
- Không còn test nào kết nối `127.0.0.1:3306` bằng `root/password` hardcode.
- Integration và API tests chạy thành công trên máy/CI chỉ với Docker và .NET SDK 10.
- Chạy test hai lần liên tiếp cho cùng kết quả và không để lại database/container.
- Test suite từ chối connection string trỏ vào database Development hoặc Production.
- Full E2E không intercept route nội bộ `/api` và chạy qua frontend/API/MySQL thật.
- GUI checkout dùng tài khoản và SKU từ seed chính thức, không hardcode GUID runtime.
- Production smoke runner không chứa request thay đổi dữ liệu.
- README mô tả riêng lệnh unit, integration, E2E, staging và production smoke.
- CI lưu artifacts cần thiết khi integration/E2E thất bại mà không lộ secrets hoặc PII.

## 13. Quyết định kiến trúc

Chọn MySQL Testcontainers làm mặc định cho integration/API tests vì vừa dùng đúng engine Production vừa cô lập và tái lập được. Database Development không phải test target. Staging là nơi duy nhất chạy full E2E với dữ liệu production-like hoặc snapshot đã ẩn danh. Production chỉ nhận smoke test read-only sau deployment.
