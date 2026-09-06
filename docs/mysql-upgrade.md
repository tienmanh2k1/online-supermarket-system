# Hướng Dẫn Nâng Cấp và Phục Hồi Database MySQL

Tài liệu hướng dẫn quy trình vận hành nâng cấp an toàn hệ thống cơ sở dữ liệu MySQL cho AptechMart, đảm bảo dữ liệu toàn vẹn giữa các phiên bản schema cũ (PascalCase) và mới (snake_case), cũng như xử lý các ràng buộc kiểm tra (CHECK constraints).

---

## 1. Nguyên Tắc An Toàn Cốt Lõi

> [!WARNING]
> **MySQL DDL Không Có Tính Giao Dịch Toàn Phần (Non-Transactional DDL)**:
> Trong MySQL (InnoDB), hầu hết các câu lệnh DDL (`ALTER TABLE`, `CREATE INDEX`, `ADD CONSTRAINT`) đều tự động thực hiện commit ngầm định (`implicit commit`) và không thể rollback trong một `BEGIN...ROLLBACK` transaction của EF Core. Nếu một bước DDL gặp lỗi, các bước trước đó đã được áp dụng vào database. Do đó:
> 1. **BẮT BUỘC sao lưu database trước khi nâng cấp**.
> 2. **Không dựa vào cơ chế transaction rollback** để hoàn tác việc nâng cấp; khi có sự cố, phục hồi từ bản sao lưu đã kiểm chứng.

---

## 2. Quy Trình Nâng Cấp Chi Tiết

Quy trình chuẩn gồm 7 bước tuần tự:

### Bước 1: Sao lưu và kiểm tra khả năng phục hồi
Thực hiện sao lưu logic toàn bộ database:
```bash
mysqldump -h <host> -P <port> -u <user> -p \
  --single-transaction \
  --routines \
  --triggers \
  --databases online_supermarket > backup_before_upgrade_$(date +%Y%m%d_%H%M%S).sql
```
*Lưu ý:* Kiểm tra file dump không rỗng và thử nghiệm phục hồi trên môi trường staging trước khi tiến hành trên production.

### Bước 2: Tạm dừng tiến trình ghi và Background Workers
- Chuyển ứng dụng hoặc API Gateway sang chế độ bảo trì (Maintenance Mode).
- Dừng các worker process (`IntelligenceWorker` và các background job).
- Đảm bảo không có transaction đang chạy:
```sql
SHOW FULL PROCESSLIST;
```

### Bước 3: Kiểm tra trạng thái metadata hiện tại
Xác định phiên bản migration hiện tại trong database:
```sql
SELECT MigrationId, ProductVersion
FROM `__EFMigrationsHistory`
ORDER BY MigrationId DESC;
```
Kiểm tra cấu trúc bảng `background_job_runs`:
```sql
SELECT COLUMN_NAME, DATA_TYPE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs';
```

### Bước 4: Chạy script tiền nâng cấp `prepare-mysql-upgrade.sql`
Script `backend/scripts/prepare-mysql-upgrade.sql` có tính chất **idempotent** (chạy nhiều lần an toàn):
- Tự động phát hiện các cột PascalCase (`JobName`, `LockKey`, `CreatedAtUtc`, `StartedAtUtc`, `CompletedAtUtc`, `ErrorSummary`, `LockToken`, `LeaseExpiresAtUtc`, `Id`, `Status`).
- Rename an toàn sang snake_case (`job_name`, `lock_key`, ...) bằng `RENAME COLUMN` của MySQL 8.0+ mà không làm mất dữ liệu, không đổi kiểu và không ảnh hưởng khóa ngoại.
- Chuẩn hóa tên unique index từ `IX_background_job_runs_JobName_LockKey` sang `IX_background_job_runs_job_name_lock_key`.
- Kiểm tra trạng thái dở dang và dừng ngay nếu phát hiện xung đột dữ liệu bất thường.

Thực hiện chạy script:
```bash
mysql -h <host> -P <port> -u <user> -p online_supermarket < backend/scripts/prepare-mysql-upgrade.sql
```

### Bước 5: Áp dụng các EF Core Migrations
Có thể chọn một trong hai phương thức:

**Cách A: Dùng EF Core CLI trực tiếp**
Luôn chỉ định connection string của database đích thông qua tham số `--connection` (hoặc thiết lập biến môi trường `ConnectionStrings__DefaultConnection`) để tránh vô tình chạy vào cấu hình mặc định:
```bash
dotnet ef database update \
  --project backend/src/OnlineSupermarket.Infrastructure \
  --startup-project backend/src/OnlineSupermarket.Api \
  --connection "Server=<host>;Port=<port>;Database=online_supermarket;User=<user>;Password=<password>;"
```

**Cách B: Sinh script SQL và chạy thủ công (Khuyến nghị cho Production)**

> [!WARNING]
> Không sử dụng cờ `--idempotent` của EF Core cho MySQL. Provider `MySql.EntityFrameworkCore` sinh ra các khối `IF NOT EXISTS(...) BEGIN ... END;` ở cấp ngoài cùng, vi phạm cú pháp MySQL (MySQL chỉ cho phép cấu trúc điều khiển `IF` bên trong Stored Programs).

Thực hiện chuẩn hóa theo quy trình 4 bước:

1. **Truy vấn migration hiện tại của database đích**:
```sql
SELECT MigrationId FROM `__EFMigrationsHistory` ORDER BY MigrationId DESC LIMIT 1;
```
*(Ghi nhận kết quả làm `<StartingMigration>`, ví dụ: `20260903161025_AddProductViewEvents`)*

2. **Sinh script SQL từ migration mốc đến mới nhất**:
```bash
dotnet ef migrations script <StartingMigration> \
  --project backend/src/OnlineSupermarket.Infrastructure \
  --startup-project backend/src/OnlineSupermarket.Api \
  -o upgrade_migrations.raw.sql
```

3. **Định dạng Delimiter cho Stored Procedures bằng công cụ chuẩn bị**:
Script do EF sinh ra chứa `CREATE PROCEDURE` với nhiều dấu chấm phẩy `;` bên trong thân procedure, khiến MySQL CLI ngắt lệnh sớm nếu thiếu lệnh `DELIMITER ;;`. Sử dụng công cụ đã chuẩn hóa:
```bash
python backend/scripts/prepare-migration-script.py upgrade_migrations.raw.sql -o upgrade_migrations.sql
```
*(Công cụ sẽ bao bọc đúng cú pháp `DELIMITER ;; ... END;; DELIMITER ;` cho từng procedure, bỏ qua chuỗi ký tự và comment, đồng thời bảo đảm tính idempotent).*

4. **Rà soát nội dung upgrade_migrations.sql và thực thi**:
```bash
mysql -h <host> -P <port> -u <user> -p online_supermarket < upgrade_migrations.sql
```

Migration `20260905094527_RestoreRecommendationConstraints` đã được thiết kế an toàn:
- Kiểm tra dữ liệu vi phạm trước khi thêm constraint.
- Kiểm tra `information_schema.TABLE_CONSTRAINTS` & `CHECK_CONSTRAINTS`: đối chiếu định nghĩa chính xác theo chuẩn AST của MySQL 8.x, chấp nhận các dạng biểu thức hợp lệ đã được kiểm chứng và từ chối mọi biểu thức `OR` hoặc biểu thức sai lệch độ ưu tiên toán tử.

### Bước 6: Smoke test hệ thống
Xác minh tính đúng đắn của cơ sở dữ liệu sau khi nâng cấp:
1. **Kiểm tra số lượng bảng nghiệp vụ** (đủ 23 bảng):
```sql
SELECT COUNT(*) FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_TYPE = 'BASE TABLE'
  AND TABLE_NAME <> '__EFMigrationsHistory';
```
*(Kết quả kỳ vọng: 23)*

2. **Kiểm tra Check Constraints trên recommendation_results**:
```sql
SELECT CONSTRAINT_NAME, ENFORCED
FROM information_schema.TABLE_CONSTRAINTS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'recommendation_results'
  AND CONSTRAINT_TYPE = 'CHECK';
```
*(Kết quả kỳ vọng: cả `ck_recommendation_results_rank` và `ck_recommendation_results_score` đều có ENFORCED = 'YES')*

3. **Kiểm tra backfill dữ liệu branch_id**:
```sql
SELECT COUNT(*) FROM background_job_runs
WHERE job_name = 'Forecast' AND lock_key LIKE 'branch:%' AND branch_id IS NULL;
```
*(Kết quả kỳ vọng: 0)*

4. **Kiểm tra toàn vẹn dữ liệu nghiệp vụ**:
- Kiểm tra số lượng bản ghi các bảng `background_job_runs`, `recommendation_results`, `demand_forecasts`.
- Thử insert dữ liệu vi phạm (`rank = 0` hoặc `score = 1.5`) xác nhận MySQL từ chối.

### Bước 7: Bật lại ứng dụng và Background Workers
- Bỏ chế độ bảo trì.
- Khởi động lại các background worker và API service.
- Quan sát logs trong 15-30 phút đầu để đảm bảo các tác vụ nền hoạt động ổn định.

---

## 3. Kế Hoạch Xử Lý Sự Cố (Rollback / Disaster Recovery)

> [!CAUTION]
> Trong MySQL, các câu lệnh DDL (`ALTER TABLE`, `CREATE TABLE`, `CREATE INDEX`) luôn thực hiện **implicit commit**. Nếu script nâng cấp gặp lỗi giữa chừng:
> - Một số lệnh DDL của migration dở dang đã được commit vào schema vật lý.
> - Tuy nhiên, bản ghi trong `__EFMigrationsHistory` chưa được ghi nhận.
> - **Tuyệt đối KHÔNG chạy lại mù quáng script dải migration**: Việc chạy lại sẽ gặp lỗi xung đột schema vật lý (như `Duplicate column name` hoặc `Duplicate key name`).

Quy trình xử lý sự cố chuẩn:
1. Dừng ngay lập tức các lệnh nâng cấp đang chạy.
2. **Phương án ưu tiên (Khuyến nghị)**: Khôi phục lại toàn bộ trạng thái database từ bản backup sạch đã tạo ở Bước 1:
```bash
mysql -h <host> -P <port> -u <user> -p online_supermarket < backup_before_upgrade_*.sql
```
3. **Phương án xử lý tại chỗ (nếu không thể restore từ backup)**:
   - Đối chiếu schema vật lý (`information_schema.COLUMNS`, `information_schema.STATISTICS`) với `__EFMigrationsHistory` để xác định chính xác câu lệnh nào đã commit dở dang.
   - Tiến hành rollback thủ công các đối tượng DDL dở dang về đúng trạng thái tương ứng với migration cuối cùng trong `__EFMigrationsHistory`.
   - Sau khi trạng thái database đã nhất quán, xác định lại `<StartingMigration>` và sinh lại script mới.
4. Khởi động lại ứng dụng ở phiên bản trước nâng cấp hoặc khắc phục lỗi gốc rễ trước khi thử nghiệm lại.
