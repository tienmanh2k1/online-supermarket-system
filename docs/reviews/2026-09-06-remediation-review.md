# Review remediation theo plan — 2026-09-06

## Phạm vi và kết luận

Review code tại HEAD `28b84e2` và working tree hiện tại, đối chiếu `docs/superpowers/plans/2026-09-06-remaining-review-findings.md`. HEAD thay đổi từ `b56b738` sang merge mới trong quá trình audit; các finding dưới đây đã kiểm tra lại với code mới. Không sửa production code trong lượt review.

**Kết luận:** chưa đạt toàn bộ plan, còn lỗi P1 trong lifecycle job, xử lý callback race, bảo vệ secret và migration upgrade. Các sửa đổi latest-payment, promotion release, guard order terminal, provider configuration và review form dùng verified ID đã xuất hiện ở merge mới; không còn coi các điểm đó là lỗi theo code cũ.

## Findings

### F1 — P1: Recovery không được nối vào startup

`backend/src/OnlineSupermarket.Infrastructure/DependencyInjection.cs:107–117`; `Jobs/JobLeaseService.cs:5`.

RecoverStaleJobsAsync có implementation nhưng tìm toàn bộ production source chỉ thấy declaration, không có caller. DI chỉ đăng ký cleanup, IntelligenceWorker và RecurringJobScheduler; JobLeaseService là scoped service không tự chạy. Sau restart, Channel trống: Queued run không được enqueue, expired Running không chuyển Failed, logical lock chặn trigger mới vô thời hạn.

Sửa: gọi recovery bằng hosted startup orchestration, bảo đảm hoàn tất trước recurring scheduling; fail stale bằng atomic predicate rồi requeue Queued. Regression phải start host mới với DB có queued/expired runs, không chỉ gọi recovery trực tiếp từ unit test. **Plan:** Tasks 6–7, R6–R7.

### F2 — P1: Heartbeat và handler dùng chung scoped DbContext đồng thời

`backend/src/OnlineSupermarket.Infrastructure/Jobs/IntelligenceWorker.cs:49–80`; `Jobs/JobRunStore.cs:7`; `DependencyInjection.cs:30,117`.

Worker resolve store và handler từ cùng scope. Cả JobRunStore lẫn forecast/recommendation handlers inject AppDbContext scoped, vì vậy share một instance. RenewLeaseLoopAsync chạy trong lúc HandleAsync query/save. Khi job kéo dài qua nhịp heartbeat và DB operation overlap, EF có thể ném lỗi second operation; nếu đang trong transaction handler, renewal còn có thể bị gộp vào transaction chưa commit. Điều này phá lease visibility và lifecycle của job dài.

Sửa: store dùng factory/context riêng mỗi operation, handler scope riêng; test handler bị giữ ở DB operation khi heartbeat tick. **Plan:** Task 5, R5.

### F3 — P1: Lease bị mất nhưng handler vẫn tiếp tục ghi kết quả

`backend/src/OnlineSupermarket.Infrastructure/Jobs/IntelligenceWorker.cs:134–138`; handler nhận stoppingToken tại dòng 80. `Recommendations/RecommendationJobHandler.cs:59–64` và `Intelligence/ForecastJobHandler.cs` commit results không kiểm tra lease ownership.

TryRenewAsync trả false chỉ làm heartbeat return. Nó không cancel handler. Khi host khác recovery đánh dấu run Failed và nhả lock, handler cũ vẫn có thể ghi recommendation/forecast rows cho run terminal. TryCompleteAsync về false sau đó không rollback batch đã commit. Đây là vi phạm invariant terminal không mutate results; đồng thời cho phép job cũ tiếp tục xử lý sau khi mất quyền sở hữu.

Sửa: linked cancellation cho handler khi renew mất lease; publish result trong transaction có kiểm tra current token/status để chặn cả trường hợp handler không kịp quan sát cancellation. Test recovery diễn ra ngay trước result commit. **Plan:** Tasks 5–7 và 16, R5/R7/R16.

### F4 — P1: Duplicate/deadlock callback chưa được phân loại và retry an toàn

`backend/src/OnlineSupermarket.Infrastructure/Payments/PaymentCallbackProcessor.cs:66–76` và duplicate lookup dòng 19–21.

Hai Serializable transactions có thể cùng đọc callback chưa tồn tại rồi tranh insert/update; deadlock victim hiện được throw ra API, không retry. Catch chỉ kiểm tra `ex.Message.Contains("Duplicate entry")`, thường không chứa lỗi provider nằm trong InnerException. Nếu text đó có mặt, mọi duplicate đều trở thành AlreadyProcessed mà không xác nhận constraint callback hoặc đọc lại winner sau rollback. Unique violation từ ledger/constraint khác vì vậy có thể bị ACK như callback đã xử lý dù transaction vừa rollback.

Sửa: classify provider error code/constraint; rollback + fresh context; duplicate callback chỉ ACK sau re-read row `(provider, externalEventId)`; deadlock retry hữu hạn từ transaction mới. Test barrier ép hai contenders cùng qua duplicate read, và unrelated unique violation không được trả AlreadyProcessed. **Plan:** Tasks 3–4, R3–R4.

### F5 — P1: Worker vẫn log nguyên exception chứa secret

`backend/src/OnlineSupermarket.Infrastructure/Jobs/IntelligenceWorker.cs:89–90,107`.

Sanitizer chỉ được dùng để tạo ErrorSummary; logger.LogError(ex, ...) vẫn serialize Exception.Message/stack/inner exception gốc. Handler ném exception chứa password, token hoặc connection string thì log vẫn ghi nguyên secret, dù cột ErrorSummary đã được redact. Điều này không đạt secret-marker log audit trong final gate.

Sửa: log exception type và sanitized summary, không truyền raw exception object ở các boundary này; áp dụng cùng nguyên tắc cho scheduler/dequeue paths có thể nhận lỗi DB/provider. Regression capture logger entry và assert seeded secret không xuất hiện trong message lẫn exception object. **Plan:** Tasks 8/16, R8/R16.

### F6 — P1: Sanitizer để lọt credential có key JSON được quote

`backend/src/OnlineSupermarket.Infrastructure/Jobs/JobErrorSanitizer.cs:14–16`.

Regex assignment yêu cầu dấu `:` hoặc `=` ngay sau key/whitespace, nhưng JSON có dấu quote kết thúc key trước `:`. Input `{"password":"REVIEW_SECRET_MARKER"}` giữ nguyên secret trong ErrorSummary. URI `?credential=REVIEW_QUERY_MARKER` cũng không được redact vì query regex chỉ nhận một danh sách key, trong khi plan yêu cầu redact URI query values.

Đã tái hiện bằng compile nguyên sanitizer source trong PowerShell với bổ sung `using System;` tương đương implicit using của project rồi gọi Sanitize. Output:

```text
Exception: {"password":"REVIEW_SECRET_MARKER"}
Exception: https://example.test/callback?credential=REVIEW_QUERY_MARKER
```

Sửa: hỗ trợ quoted keys/values hoặc safe allow-list fallback, redact URI query values trước truncation. Thêm regression cho JSON/quoted keys, multiline và inner exception; xử lý maxLength 0–2 để tránh substring âm. **Plan:** Task 8, R8.

### F7 — P1: Migration chưa tự normalize background job schema như plan

`backend/src/OnlineSupermarket.Infrastructure/Persistence/Migrations/20260905081854_SyncModelAndMigrations.cs:11–14`; `docs/mysql-upgrade.md:54–63`.

SyncModelAndMigrations.Up vẫn rỗng; migration tiếp theo RestoreRecommendationConstraints chỉ sửa recommendation checks. Không có migration normalize PascalCase background_job_runs sang snake_case trong chain đang review, trong khi configuration đọc snake_case. Runbook vẫn yêu cầu prepare-mysql-upgrade.sql chạy trước. Database legacy không chạy pre-script vì thế không được chứng minh hội tụ bằng Database.MigrateAsync; có nguy cơ lỗi unknown column khi chạy worker/API.

Sửa: convergence migration forward-only có schema preflight và preserve data, kiểm thử legacy qua Database.MigrateAsync không pre-script. Working tree còn thay đổi 24 historical migration files và snapshot (25 files tổng cộng); đây là thay đổi đã có từ trước, nhưng quality gate byte-for-byte immutable history vẫn chưa đạt. Không commit các diff lịch sử đó như release fix. **Plan:** Tasks 13–15, R13–R15.

## Acceptance gaps bổ sung

- Task 16 chưa có compose.e2e.yaml, run-remediation-e2e-tests.mjs, MySqlRemediationFlowTests.cs hoặc npm script test:e2e:remediation trong source đang review. Runbook/commit title không thay thế executable E2E gate.
- JobRunCoordinator vẫn dùng DateTime.UtcNow, chưa inject TimeProvider; channel errors bị catch im lặng. Worker renew mỗi lease/2 và lifecycle vẫn ở worker, chưa theo executor/factory contract Task 5. Đây là các điểm cần đối chiếu tiếp, ngoài các bug P1 ở trên.
- Payment race test hiện khởi động hai tasks nhưng chưa có barrier buộc race; test pass một lần chưa chứng minh đường deadlock/unique loser.

## Kiểm chứng đã chạy

1. `dotnet test backend/tests/OnlineSupermarket.Api.Tests --no-restore --filter "FullyQualifiedName~InventoryMutationEndpointTests|FullyQualifiedName~ReviewEndpointsTests|FullyQualifiedName~RecommendationEndpointsTests"`: **34 passed, 0 failed**.
2. `dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests --no-restore --filter "FullyQualifiedName~MySqlPaymentCallbackTests|FullyQualifiedName~MySqlJobRunLifecycleConcurrencyTests"`: **11 failed tại khởi tạo fixture**, DockerEndpointAuthConfig/Testcontainers báo Docker unavailable/misconfigured. Đã thử lại ngoài sandbox, vẫn lỗi khởi tạo. Không coi đây là 11 lỗi nghiệp vụ, cũng không coi concurrency gate đã pass.
3. Sanitizer reproduction F6: hai synthetic secret markers còn nguyên trong output.
4. Static call-site inspection: production source không gọi RecoverStaleJobsAsync; DI dùng AddDbContext scoped và JobRunStore scoped; frontend/package.json không có remediation E2E script.

Không chạy full frontend/full .NET suite hoặc migration upgrade gates trong review này. Không kết luận các phần chưa kiểm chứng là an toàn. Plan tiếp nối nằm tại `docs/superpowers/plans/2026-09-06-remediation-continuation.md`; dùng finding ở HEAD mới này để tránh sửa lại code đã được merge.
