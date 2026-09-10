# Gate C Release Test Report

Ngày chạy: 2026-09-09  
HEAD khi kiểm tra: `42ca597`  
Môi trường: Docker Compose `online-supermarket_default`, API `http://localhost:8080`, frontend `http://localhost:5173`, MySQL `OnlineSupermarket`.

## Kết quả

| Nhóm | Kết quả | Evidence |
|---|---:|---|
| Release smoke runner | 12/12 pass, 0 fail, 0 skip | `artifacts/release/2026-09-09/gate-c/release-smoke-results.json` |
| API: reviews, inventory, payment, recommendation, forecast | 65/65 pass | `artifacts/release/2026-09-09/gate-c/gate-c-api.trx` |
| Infrastructure/MySQL: ledger, callback, recommendation, forecast | 70/70 pass | `artifacts/release/2026-09-09/gate-c/gate-c-infrastructure.trx` |
| UI: reviews, inventory, forecast, product detail, sales report | 65/65 pass | Vitest output ngày 09/09 |
| Runner contract | 2/2 pass | `node --test scripts/run-e2e-release-smoke.test.mjs` |

## Ma trận 12 smoke cases

| ID | Flow | Loại | Kết quả kiểm chứng |
|---|---|---|---|
| C-01 | Commerce | Happy | Completed purchase review: tạo, sửa và đọc aggregate trên PDP |
| C-02 | Commerce | Edge | Hết stock trả conflict, rollback order và ledger |
| C-03 | Commerce | Edge | Pending order, sai owner và duplicate review bị từ chối |
| C-04 | Commerce | Edge | Callback race/duplicate/wrong method và failure release idempotent trên MySQL |
| C-05 | Recommendation | Happy | Anonymous view được merge vào JWT owner; merge lặp trả 0 |
| C-06 | Recommendation | Edge | Training failure không phá batch đã publish |
| C-07 | Recommendation | Edge | Cold-start dùng fallback; item không hợp lệ/hết stock bị loại |
| C-08 | Recommendation | Edge | Branch không tồn tại bị từ chối; body không thể giả owner |
| C-09 | Forecast | Happy | Ghi SMA 7/14 và tính theo sold quantity |
| C-10 | Forecast | Edge | Thiếu lịch sử ghi `InsufficientData` |
| C-11 | Forecast | Edge | Horizon ngoài 7/14 trả bad request |
| C-12 | Forecast | Edge | Run và kết quả cô lập theo branch |

Runner kiểm tra API và DB container cùng network/database trước mọi case, health API phải trả thành công, và mở trang frontend thật tương ứng bằng Playwright. Runner trả exit code khác 0 nếu có fail hoặc skip; lần chạy nghiệm thu trả exit code 0.

## Regression bổ sung

- Payment callback: duplicate, invalid signature/payload, wrong amount/method, concurrent callback và exactly-once effects.
- Inventory: reserve, sale, cancel/failure release, manual adjustment, operation-key idempotency, rollback khi ledger insert lỗi và tranh stock.
- Authorization/ownership: 401/403 cho API admin; review và anonymous merge không nhận owner từ request body.
- UI: branch switching bỏ stale response; review capture failure không làm hỏng PDP; forecast/sales/inventory có loading, error và empty state.

## Kết luận

Gate C đạt: 12/12 smoke pass, toàn bộ regression mục tiêu pass, không còn blocker đã biết. Feature freeze có hiệu lực sau checkpoint ngày 09/09; mọi sửa code sau thời điểm này phải chạy lại regression liên quan và smoke gate.
