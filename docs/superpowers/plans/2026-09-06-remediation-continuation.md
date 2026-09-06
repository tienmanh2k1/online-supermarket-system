# Remediation Continuation Implementation Plan

> **For agentic workers:** Use `superpowers:executing-plans`. Thực hiện tuần tự từng task, tự review sau mỗi task, chỉ chuyển tiếp khi quality gate của task đạt. Checkbox chỉ đánh dấu khi có bằng chứng. Không tự chuyển sang chạy song song.

**Goal:** Hoàn thiện toàn bộ 16 task của plan gốc, gồm cả các phần đã commit nhưng chưa đáp ứng acceptance criteria.

**Architecture:** Xác minh callback theo từng provider, xử lý hiệu ứng trong một transaction có kiểm soát race; executor sở hữu lifecycle job và store cập nhật bằng predicate atomic. Hoàn thiện domain/API/UI rồi mới chốt model và tạo migration hội tụ forward-only, cuối cùng kiểm chứng trên MySQL 8.4 và browser.

**Tech Stack:** .NET 10, EF Core 10/MySql.EntityFrameworkCore, MySQL 8.4, xUnit/Testcontainers, React/TypeScript, Vitest/Testing Library, Playwright.

**Spec:** [Plan gốc](2026-09-06-remaining-review-findings.md), [contract hiện tại](../../tasks/remaining-findings-contracts.md), sáu task board được liệt kê trong plan gốc. Tài liệu này điều chỉnh trạng thái và thứ tự thực thi; mọi yêu cầu trong plan gốc vẫn bắt buộc.

## Baseline và đính chính trạng thái

**Cập nhật review:** Trong khi lập tài liệu, HEAD đã chuyển tới `28b84e2` (merge `fix/remaining-review-findings-v2`). Bảng bên dưới là audit lịch sử tại `b56b738`, không phải danh sách lỗi còn tồn tại ở HEAD mới. Đọc [báo cáo review mới](../../reviews/2026-09-06-remediation-review.md) trước khi thực thi; với mỗi R-task, giữ phần đã chứng minh đúng và chỉ sửa finding hoặc bổ sung bằng chứng còn thiếu. Không lặp lại những sửa đổi provider fields, promotion release và review UI đã có trong merge mới.

Thứ tự thực thi R1–R16 giữ nguyên; trạng thái sau merge cần đóng bằng gate, không bằng tên commit. Ưu tiên trong R3–R8: callback race classification/retry, shared DbContext, startup recovery, lease-loss cancellation, sanitized logging. R13–R16 vẫn cần chứng minh immutable history, tự hội tụ không pre-script và E2E runnable.

Audit ngày 2026-09-06 trên `main`, HEAD `b56b738`. Worktree riêng đã được người dùng đồng ý nhưng lượt triển khai trước không tạo; các commit dưới đây nằm trên `main`. Không rewrite hoặc reset lịch sử này.

| Task gốc | Commit đã có | Trạng thái thực tế / phần còn thiếu |
| --- | --- | --- |
| 1 | `93e5cf5` | Có tài liệu, nhưng cột khóa chính bị ghi `run_id` thay vì `id`; thiếu danh sách schema chính xác và review đầy đủ sáu spec. |
| 2 | `8a3c9aa` | Chưa đạt: canonicalization VNPay chọn signature key dựa trên giá trị signature; MoMo dùng sort chung; thiếu status không bị reject; secret rỗng vẫn dùng được; config khác plan; payload lưu nguyên signature; test thiếu ma trận provider. |
| 3 | `0577c0b` | Chưa đạt: chưa chọn payment mới nhất, chưa guard order terminal, chưa release promotion usage, chưa xử lý unique/deadlock race; malformed callback bị map 401 trước error 400; chưa có endpoint regression tests. |
| 4 | — | Chưa có `MySqlPaymentCallbackTests.cs`. |
| 5–7 | — | Chưa triển khai executor/store/recovery mới và concurrency gate. |
| 8 | `4aeab13` | Đã đổi CompletedAtUtc; sanitizer còn thiếu URI query/quoted credentials/length boundary; thiếu cross-midnight regression và quality gate. |
| 9 | `53e5440` | Có domain guards và hai test; thiếu ledger equations, rollback và API regression gate. |
| 10 | `b56b738` | Có backend guards và tham số client; ProductReviews vẫn gọi eligibility không có target và dùng `targetOrderItemId || eligibility.orderItemId`; thiếu các regression mới. |
| 11 | `47c4319` | Có hai guards; thiếu API branch regression và toàn bộ selected gate. |
| 12–16 | — | Chưa hoàn tất polling, migration/recovery runbook và integration gate. |

Các test pass được báo ở lượt trước là focused tests, không phải bằng chứng toàn bộ task đạt. Không ghi nhận các task 2, 3, 8, 9, 10, 11 là hoàn tất chỉ vì đã commit.

## Global Constraints

- Giữ thứ tự `R1 → R2 → R3 → R4 → R5 → R6 → R7 → R8 → R9 → R10 → R11 → R12 → R13 → R14 → R15 → R16`.
- Mỗi task: test tái hiện lỗi → xác nhận RED đúng nguyên nhân → sửa tối thiểu → GREEN → tự review spec + diff → commit riêng. Nếu code đã sửa trước test, chứng minh test phát hiện regression bằng tạm bỏ đúng guard trong workspace cô lập rồi phục hồi; không reset thay đổi người dùng.
- Không bỏ qua task khó để làm task nhỏ phía sau. Lỗi môi trường phải ghi đúng là blocked gate, không skip thành pass.
- Các sửa đổi migration/config/test fixture chưa commit đã tồn tại trước lượt triển khai. Giữ riêng, không `git add .`, không commit migration lịch sử bị sửa hoặc provider source/artifacts như một checkpoint chung.
- Mốc bất biến cho migration lịch sử là `deeccfd` (trước các commit remediation); ghi thêm SHA đầy đủ khi bắt đầu. Migrations đến `20260904160056_AddBackgroundJobRunBranch` phải khớp mốc này.
- Không xóa hai migration chưa commit cho đến khi xác minh deployed inventory. Nếu chưa có bằng chứng, giữ chúng và chặn quyết định xóa; có thể chuẩn bị convergence/test trong sandbox database.
- Các check DB dùng MySQL 8.4 thật. Không dùng database ứng dụng làm fixture. Secret test chỉ dùng với database/container disposable.
- Không lưu signature/secret/token/raw stack trace trong API, logs, PaymentCallback.RawResponse, Payment.ProviderResponse hoặc ErrorSummary.
- Tất cả lệnh dưới đây chạy tại repo root; npm dùng `--prefix frontend`. Nếu sandbox chặn Docker hoặc restore, dùng escalation theo cơ chế công cụ.
- Chỉ một task hoàn tất được commit; stage danh sách file tường minh rồi kiểm tra `git diff --cached --name-only` và `git diff --cached --check`.

## R1 — Chốt workspace, contract và baseline (Task gốc 1)

**Files:** sửa `docs/tasks/remaining-findings-contracts.md`; cập nhật checklist tài liệu này. Kiểm kê `.gitignore`, migrations và các untracked file, chưa xóa.

**Interfaces:** contract là đầu vào cho tất cả task sau; giữ callback envelope `{ provider, data }` và các status code đã công bố.

- [ ] Ghi `git status --short`, `git log -8 --oneline`, SHA `deeccfd`, danh sách staged/untracked. Xác nhận không có thay đổi mới ngoài baseline đã audit.
- [ ] Tạo worktree cô lập theo chấp thuận đã có. Chuyển bản sao thay đổi cần thiết bằng patch tracked + copy tường minh các fixture/script/docs cần thiết; so sánh hash nguồn/đích. Giữ workspace nguồn nguyên trạng, không stash/drop. Không mang bin/obj/node_modules/artifacts/provider source nếu không cần; giữ csproj exclusions tương ứng cho đến cleanup task.
- [ ] Đọc nội dung task của sáu HTML board (bỏ CSS khi trích xuất), đối chiếu contract. Canonical columns phải là `id, job_name, lock_key, branch_id, status, created_at_utc, started_at_utc, completed_at_utc, error_summary, lock_token, lease_expires_at_utc`; không tự thêm `run_id` hoặc result columns.
- [ ] Chốt config đúng plan: `Payments:Webhooks:VNPay:Secret`, `Payments:Webhooks:MoMo:Secret`. Chốt malformed field → 400; unknown provider/invalid signature → 401; lỗi cấu hình verifier trả lỗi server an toàn, không giả thành chữ ký hợp lệ. Duplicate chỉ là callback hợp lệ đã commit.
- [ ] Xác định quy tắc COD `PendingCollection` từ call sites trước khi siết `Payment` theo Pending/Processing; giữ luồng thu tiền COD hợp lệ bằng transition rõ ràng nếu được sử dụng.
- [ ] Chạy baseline build, focused tests hiện có; ghi số pass/fail và lỗi baseline. Không tuyên bố full gate sạch.

```powershell
dotnet build OnlineSupermarket.slnx --no-restore
dotnet test backend/tests/OnlineSupermarket.Domain.Tests --no-restore
git diff --check -- docs/tasks/remaining-findings-contracts.md
```

**Tự review:** schema đúng configuration; HTTP table không mâu thuẫn thứ tự verification; bản sao workspace đầy đủ. **Commit:** `docs: correct remediation contracts and baseline`.

## R2 — Hoàn thiện provider verification (Task gốc 2)

**Files:** sửa `backend/src/OnlineSupermarket.Infrastructure/Payments/{CallbackVerifierSupport,IPaymentCallbackVerifier,PaymentCallbackVerificationResult,PaymentWebhookOptions,VnPayCallbackVerifier,MomoCallbackVerifier}.cs`, `backend/src/OnlineSupermarket.Infrastructure/DependencyInjection.cs`, `backend/src/OnlineSupermarket.Domain/Payments/Payment.cs`; test `backend/tests/OnlineSupermarket.Infrastructure.Tests/Payments/PaymentCallbackVerifierTests.cs`, `backend/tests/OnlineSupermarket.Domain.Tests/Payments/PaymentTests.cs`.

**Interfaces:** giữ `PaymentCallbackVerificationResult(bool IsValidSignature, string ExternalEventId, Guid OrderId, decimal Amount, bool IsSuccess, string SanitizedPayload, string? ErrorCode)`; ErrorCode phân biệt malformed/signature/configuration, không mang exception detail.

- [ ] Xác minh canonical string/encoding/amount unit và callback field names theo tài liệu chính thức provider tại thời điểm thực thi. Lưu URL và version trong test fixture notes; dùng fixed known-answer vectors, không dùng helper production để tạo expected signature.
- [ ] Thêm RED cases cho VNPay/MoMo: chữ ký hợp lệ, tamper 1 byte, malformed hex, sai length, thiếu signature/event/order/amount/status, GUID rỗng, amount âm/overflow/culture, unknown code, secret rỗng, extra field chứa secret.
- [ ] VNPay: chỉ ký tập `vnp_*` đúng protocol, loại `vnp_SecureHash` và `vnp_SecureHashType` bằng key; sort ordinal, encode theo provider, normalize amount unit đúng tài liệu. Không dùng `signature.Contains(...)` để chọn key.
- [ ] MoMo: sử dụng ordered fields và tên trường đúng callback protocol; không dùng canonical sort chung hoặc tự chấp nhận alias chưa xác minh. Nếu cần access key/partner code, bind cấu hình riêng và test cấu hình thiếu.
- [ ] Validation cấu trúc tối thiểu trả malformed; xác minh signature trước khi sử dụng dữ liệu normalized cho nghiệp vụ; parse decimal với grammar rõ ràng và InvariantCulture, không cho phép separator văn hóa ngoài protocol.
- [ ] Tạo SanitizedPayload bằng allow-list giá trị đã xác minh, không serialize request dictionary. Thêm test toàn bộ secret marker không xuất hiện trong payload. Empty secret fail closed.
- [ ] Thêm domain tests riêng cho Pending→Completed/Failed, Completed→Completed/Failed, Failed→Failed/Completed; invalid transition giữ nguyên status, transaction ID, response, timestamp; test Processing/COD theo contract R1.

```powershell
dotnet test backend/tests/OnlineSupermarket.Domain.Tests --filter FullyQualifiedName~PaymentTests
dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests --filter FullyQualifiedName~PaymentCallbackVerifierTests
```

**Tự review:** known-answer test độc lập implementation; config keys đúng; payload không lộ secret; không còn missing status default. **Commit:** `fix(payment): complete provider verification contracts`.

## R3 — Hoàn thiện endpoint và transactional processor (Task gốc 3)

**Files:** sửa `backend/src/OnlineSupermarket.Api/Endpoints/CheckoutEndpoints.cs`, `backend/src/OnlineSupermarket.Infrastructure/Payments/{IPaymentCallbackProcessor,PaymentCallbackProcessor}.cs`; tạo `backend/tests/OnlineSupermarket.Api.Tests/Endpoints/PaymentCallbackEndpointsTests.cs`; sửa `backend/tests/OnlineSupermarket.Api.Tests/Endpoints/InventoryMutationEndpointTests.cs` nếu fixture cũ gửi callback unsigned.

**Interfaces:** `ProcessAsync(string provider, PaymentCallbackVerificationResult callback, CancellationToken)` trả Processed/AlreadyProcessed/PaymentNotFound/Conflict. Endpoint truyền canonical provider từ verifier.

- [ ] Thêm endpoint tests với fake verifier/processor: unknown provider 401, malformed 400, invalid signature 401, null data 400, safe config failure; mọi reject không gọi processor. Test đủ bốn outcome và không lộ verifier detail.
- [ ] Sửa thứ tự error mapping: MALFORMED_CALLBACK phải được xét trước nhánh chung `!IsValidSignature`. Validate Guid/event/amount của result trước delegation, không phản chiếu ErrorCode tùy ý từ exception ra response.
- [ ] Trong transaction: duplicate check; chọn latest payment theo `CreatedAtUtc` và tie-breaker ổn định; validate provider/method/amount; load order/items/history; chỉ chấp nhận order state được contract cho phép trước khi attach callback.
- [ ] Success chỉ Pending order→Confirmed. Failure: Release mỗi inventory đủ số lượng đúng operation key, release promotion usage đúng một lần, order→Cancelled. Missing inventory phải rollback toàn bộ thay vì bị bỏ qua bởi `.Where(inventories.ContainsKey)`.
- [ ] Thêm tests order Completed/Cancelled/Failed không bị chuyển ngược, wrong amount/provider không ghi callback, nhiều payment chọn latest, missing inventory không partial effect, failed callback trả lại promotion usage, exception giữa transaction rollback payment/order/ledger/promotion.
- [ ] Không trả outcome rồi để entity Added/Modified còn treo có thể SaveChanges ngoài transaction; rollback/dispose và clear hoặc dùng scope cô lập cho failed attempt. Kiểm tra direct processor call không chấp nhận kết quả unverified.

```powershell
dotnet test backend/tests/OnlineSupermarket.Api.Tests --filter FullyQualifiedName~PaymentCallbackEndpointsTests
dotnet test backend/tests/OnlineSupermarket.Api.Tests --filter FullyQualifiedName~InventoryMutationEndpointTests
```

**Tự review:** mỗi outcome đúng response table; tất cả hiệu ứng business thuộc cùng transaction; failure trả promotion; không chỉ dựa vào build. **Commit:** `fix(payment): restore complete atomic callback effects`.

## R4 — Chứng minh callback trên MySQL (Task gốc 4)

**Files:** tạo `backend/tests/OnlineSupermarket.Infrastructure.Tests/Persistence/MySqlPaymentCallbackTests.cs`; sửa `MySqlFixture.cs` cùng thư mục chỉ để hỗ trợ database cô lập; sửa `PaymentCallbackProcessor.cs` khi test race yêu cầu.

**Interfaces:** fixture cấp connection riêng cho mỗi test; mỗi contender dùng AppDbContext riêng. Concurrency dùng barrier/TaskCompletionSource, không hy vọng race xuất hiện nhờ sleep.

- [ ] Seed user, branch, product, inventory reserved, promotion đã dùng, Pending order/payment bằng helpers test thực tế. Fixture failure do migration phải được ghi nhận; DB test business có thể tạo schema từ model trong database disposable riêng, nhưng không tính là migration gate.
- [ ] Test sequential duplicate, concurrent duplicate, success/failure concurrent, repeated failure, wrong amount, callback sau terminal. Assert count callback=1 cho duplicate, một terminal effect, status order/payment nhất quán, reservation/ledger/promotion chính xác.
- [ ] Thêm row-lock hoặc retry database có predicate, không process lock. Retry deadlock chỉ với transaction mới, trạng thái tracked mới và giới hạn hữu hạn. Unique conflict chỉ chuyển AlreadyProcessed sau rollback và re-read row thắng; lỗi DB khác phải propagate an toàn.
- [ ] Chạy test race nhiều lần trong cùng một gate có số lần cố định; teardown database/container trong finally, kiểm tra không để orphan.

```powershell
dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests --filter FullyQualifiedName~MySqlPaymentCallbackTests
dotnet test backend/tests/OnlineSupermarket.Domain.Tests --filter FullyQualifiedName~PaymentTests
dotnet test backend/tests/OnlineSupermarket.Api.Tests --filter FullyQualifiedName~PaymentCallbackEndpointsTests
dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests --filter FullyQualifiedName~PaymentCallbackVerifierTests
```

**Tự review:** dùng hai connections thật; losing transaction không để hiệu ứng; không catch mọi DbUpdateException như duplicate. **Commit:** `test(payment): prove callback concurrency on mysql`.

## R5 — Atomic job store và executor (Task gốc 5)

**Files:** tạo `backend/src/OnlineSupermarket.Infrastructure/Jobs/{IJobRunStore,EfJobRunStore,JobRunExecutor}.cs`; sửa `IntelligenceWorker.cs`, `DependencyInjection.cs`; tạo `backend/tests/OnlineSupermarket.Infrastructure.Tests/Jobs/JobRunExecutorTests.cs`; sửa `IntelligenceWorkerTests.cs`.

**Interfaces:** IJobRunStore có `Task<bool>` TryStartAsync/TryRenewAsync/TrySucceedAsync/TryFailAsync với runId, token, nowUtc, leaseUntil khi cần, CancellationToken. Executor `Task ExecuteAsync(JobRequest request, CancellationToken cancellationToken)`. Handler vẫn `HandleAsync(Guid runId, CancellationToken)`.

- [ ] RED cases: success, throw, unknown handler, duplicate request, host cancellation, heartbeat loss, MaxConcurrentJobs và DI host startup bật background services.
- [ ] TryStart dùng predicate Queued; renew dùng Running+token+unexpired lease; terminal dùng Running+token và ownership rule R1. affected-row count là kết quả duy nhất. Terminal atomically set completed_at_utc, null lease/token, lock_key=`released:{id}`.
- [ ] Executor tạo token `Guid.NewGuid().ToString("N")`, start trước dispatch; handler ở scope mới; heartbeat max(1 second, lease/3), context riêng mỗi DB operation. Mất lease thì cancel handler và cấm terminal/result overwrite. Await heartbeat cleanup trong finally.
- [ ] Kiểm tra handler forecast/recommendation có tự mutate terminal hoặc publish results không: chuyển lifecycle về executor; ràng buộc publish batch vào ownership hiện tại để handler mất lease không ghi kết quả terminal. Bổ sung regression nếu cần sửa handler.
- [ ] Worker chỉ đọc queue và quản lý semaphore từ validated options; shutdown dừng nhận việc, await active tasks; host cancellation để Running cho recovery, lỗi handler thường chuyển Failed sanitized.
- [ ] Đăng ký official AddDbContextFactory với options lifetime phù hợp, giữ scoped AppDbContext cho endpoints; test real service provider để bắt singleton/scoped mismatch.

```powershell
dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests --filter "FullyQualifiedName~JobRunExecutorTests|FullyQualifiedName~IntelligenceWorkerTests"
```

**Tự review:** không share DbContext giữa heartbeat/handler; token guards trong SQL; shutdown không bị biến thành success; result publication vẫn an toàn. **Commit:** `fix(jobs): own run lifecycle in executor`.

## R6 — Startup recovery (Task gốc 6)

**Files:** tạo `backend/src/OnlineSupermarket.Infrastructure/Jobs/JobRecoveryHostedService.cs`, `backend/tests/OnlineSupermarket.Infrastructure.Tests/Jobs/JobRecoveryHostedServiceTests.cs`; sửa `JobLeaseService.cs`, `IJobRunStore.cs`, `EfJobRunStore.cs`, `DependencyInjection.cs`, `JobLeaseTests.cs`.

**Interfaces:** store thêm recovery atomic trả số expired run đã fail; query Queued requests giữ nguyên runId. IJobQueue chỉ đánh thức worker.

- [ ] RED cases queued trước restart, unexpired untouched, expired failed/released, terminal không enqueue, recovery lặp lại.
- [ ] Recovery predicate gồm Running và expiry vẫn hết hạn tại thời điểm UPDATE; không read rồi SaveChanges stale entity. Sau đó requeue Queued bằng ID hiện có.
- [ ] Chốt startup thực sự await recovery trước recurring scheduler; registration order một mình chưa đủ nếu StartAsync trả sớm. Kiểm tra bounded channel không deadlock khi queued count vượt capacity: consumer có thể drain nhưng dispatch/scheduler tuân gate đã chốt.
- [ ] Nhiều host có thể enqueue trùng; TryStart chỉ cho một handler chạy. Test backlog lớn hơn channel capacity và host shutdown trong recovery.

```powershell
dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests --filter "FullyQualifiedName~JobLeaseTests|FullyQualifiedName~JobRecoveryHostedServiceTests"
```

**Tự review:** không tạo run mới cho queued cũ; recovery không hồi sinh terminal; không startup deadlock. **Commit:** `fix(jobs): recover persisted runs on startup`.

## R7 — MySQL lease/queue concurrency (Task gốc 7)

**Files:** sửa `backend/tests/OnlineSupermarket.Infrastructure.Tests/Persistence/MySqlJobRunConcurrencyTests.cs`, `backend/src/OnlineSupermarket.Infrastructure/Jobs/{JobRunCoordinator,JobLeaseService,EfJobRunStore}.cs`.

**Interfaces:** coordinator nhận TimeProvider; DB unique `(job_name, lock_key)` quyết định một active run.

- [ ] RED tests: concurrent trigger, hai workers start cùng run, stale recovery vs renewal, completion vs recovery, channel enqueue thất bại rồi process mới recovery.
- [ ] Mỗi contender dùng connection/context riêng; barrier kiểm soát read/update overlap. Assert đúng winning predicate, một active lock và đúng một terminal transition.
- [ ] Enqueue fail sau commit vẫn giữ Queued; log warning đã sanitize, không xóa durable row; process mới phải chạy được run đó. Thời gian lấy từ TimeProvider.
- [ ] Retry DB conflict chỉ sau rollback và re-read; không thêm semaphore/process lock để làm test pass giả.

```powershell
dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests --filter FullyQualifiedName~MySqlJobRunConcurrencyTests
```

**Tự review:** renewal thắng thì không bị stale-fail; terminal không bị ghi đè; queue loss recover được. **Commit:** `test(jobs): prove atomic lease and queue races`.

## R8 — Sanitizer và completion-day scheduling (Task gốc 8)

**Files:** sửa `backend/src/OnlineSupermarket.Infrastructure/Jobs/JobErrorSanitizer.cs`, `backend/src/OnlineSupermarket.Infrastructure/Intelligence/ForecastRecurringSchedule.cs`, `backend/tests/OnlineSupermarket.Infrastructure.Tests/Jobs/JobErrorSanitizerTests.cs`, `backend/tests/OnlineSupermarket.Infrastructure.Tests/Intelligence/ForecastRecurringScheduleTests.cs`.

- [ ] RED sanitizer cases: Password="two words", bearer, connection-string credentials, URI query api_key/access_token và arbitrary query value, multiline/inner exception, exception message chứa stack frame, maxLength 0/1/2/3/1000. Chốt maxLength âm throw ArgumentOutOfRangeException; nonnegative không throw do substring âm.
- [ ] Dùng exception type + safe message, allow-list/fallback message với loại lỗi không tin cậy; redact trước truncate, một dòng tối đa 1000 ký tự; không Exception.ToString().
- [ ] Seed queued trước midnight/completed sau midnight và verify không rerun ngày completion. Test completed hôm qua, CompletedAtUtc null, giờ trước lịch và active lock. Dữ liệu thời gian bất thường chỉ tạo trong test fixture, không nới domain API.
- [ ] Chạy full workstream job gate bên dưới, gồm MySQL thật.

```powershell
dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests --filter "FullyQualifiedName~JobRun|FullyQualifiedName~JobLease|FullyQualifiedName~IntelligenceWorker|FullyQualifiedName~JobErrorSanitizer|FullyQualifiedName~ForecastRecurringSchedule|FullyQualifiedName~JobRecovery"
```

**Tự review:** log/persisted summary không chứa bất kỳ seeded marker; schedule dựa completion. **Commit:** `fix(jobs): finish error redaction and schedule regressions`.

## R9 — Ledger và rollback regressions (Task gốc 9)

**Files:** sửa `backend/src/OnlineSupermarket.Infrastructure/Inventory/InventoryMutationService.cs` khi test chứng minh cần; bổ sung `backend/tests/OnlineSupermarket.Domain.Tests/Inventory/BranchInventoryTests.cs`, `backend/tests/OnlineSupermarket.Infrastructure.Tests/Inventory/InventoryMutationServiceTests.cs`, `backend/tests/OnlineSupermarket.Api.Tests/Endpoints/InventoryMutationEndpointTests.cs`.

- [ ] Thêm exact boundaries Release(reserved), AdjustQuantity(reserved), overflow release và onHand dưới reserved; invalid call giữ cả quantities/timestamp.
- [ ] Test reserve/release/sale/manual adjustment bằng snapshot trước/sau, assert cho mỗi ledger row:

```text
beforeOnHand + quantityOnHandDelta = quantityOnHandAfter
beforeReserved + reservedQuantityDelta = reservedQuantityAfter
quantityOnHandAfter >= reservedQuantityAfter >= 0
```

- [ ] Invalid batch item thứ hai rollback cả item thứ nhất; duplicate operation_key không sinh row thứ hai; đọc lại bằng context mới để tránh chỉ assert tracked state. API invalid adjustment trả controlled error và không thay đổi DB.
- [ ] Đồng bộ preflight và domain guards nếu test fail, không khôi phục clamping.

```powershell
dotnet test OnlineSupermarket.slnx --no-restore --filter "FullyQualifiedName~BranchInventoryTests|FullyQualifiedName~InventoryMutation"
```

**Tự review:** đủ domain/infrastructure/API, ledger delta là hiệu ứng thật. **Commit:** `test(inventory): prove ledger equations and rollback`.

## R10 — Review trust boundary end-to-end (Task gốc 10)

**Files:** sửa `frontend/src/features/reviews/ProductReviews.tsx`, `frontend/src/features/products/ProductDetailPage.tsx`, `frontend/src/api/reviewApi.ts`, `backend/src/OnlineSupermarket.Api/Endpoints/ReviewEndpoints.cs` khi cần; test `frontend/src/features/reviews/ProductReviews.test.tsx`, `frontend/src/features/products/ProductDetail.test.tsx` (tên hiện có), `backend/tests/OnlineSupermarket.Api.Tests/Endpoints/ReviewEndpointsTests.cs`.

- [ ] RED API: target sai product, sai user, incomplete, reviewed, missing, valid completed; page=int.MaxValue, empty; inactive review by ID. Xác minh mismatch không tiết lộ thông tin không cần thiết về item người khác.
- [ ] RED UI: target URL sai bị backend reject → không có form; target hợp lệ → request có query và submit đúng ID verified; changing target hủy request cũ; existing-review behavior được giữ.
- [ ] Truyền target qua tham số thứ tư đang có, chỉ render khi CanReview và OrderItemId tồn tại:

```tsx
reviewApi.getEligibility(productId, accessToken, signal, targetOrderItemId)
// Form receives eligibility.orderItemId only after canReview and non-null checks.
```

- [ ] Xóa fallback `targetOrderItemId || eligibility.orderItemId`; backend rejection hiển thị thông báo invalid-link ổn định. URL ID không bao giờ trở thành quyền tạo review.
- [ ] Pagination thêm tie-breaker ổn định nếu required để tránh nhảy item cùng timestamp; giữ clamp count trước Skip. Active product join giữ nguyên.

```powershell
dotnet test backend/tests/OnlineSupermarket.Api.Tests --filter FullyQualifiedName~ReviewEndpointsTests
npm --prefix frontend test -- --run ProductDetail.test.tsx ProductReviews.test.tsx
```

**Tự review:** không chỉ sửa signature API client; component thực sự gửi target và chỉ dùng verified ID. **Commit:** `fix(reviews): enforce verified deep links in UI`.

## R11 — Branch và enum regression gate (Task gốc 11)

**Files:** bổ sung `backend/tests/OnlineSupermarket.Api.Tests/Endpoints/RecommendationEndpointsTests.cs`, `backend/tests/OnlineSupermarket.Domain.Tests/Intelligence/DemandForecastTests.cs`; production guards hiện có trong RecommendationEndpoints.cs/DemandForecast.cs chỉ sửa nếu test fail.

- [ ] Unknown branch → 404 + code BRANCH_NOT_FOUND, count ProductViewEvents không tăng; omitted branch accepted; valid branch giữ reference; request invalid session vẫn theo contract cũ.
- [ ] Undefined enum 999 và -1 throw, cả ba giá trị hợp lệ accepted; giữ validations horizon/quantity/date/UTC/algorithm.
- [ ] Chứng minh guard regression bị test bắt, chạy gate và tự review payload code đúng client expectations.

```powershell
dotnet test OnlineSupermarket.slnx --no-restore --filter "FullyQualifiedName~RecommendationEndpointsTests|FullyQualifiedName~DemandForecastTests"
```

**Commit:** `test(intelligence): cover invalid branch and forecast quality`.

## R12 — Shared bounded polling (Task gốc 12)

**Files:** tạo `frontend/src/api/jobApi.ts`, `frontend/src/features/admin/jobPolling.ts`, `jobPolling.test.ts`; sửa `frontend/src/api/{recommendationApi,inventoryIntelligenceApi}.ts`, `frontend/src/features/admin/{AdminRecommendationsPage,AdminForecastPage}.tsx` và hai `.test.tsx` tương ứng.

**Interfaces:** copy shape JSON hiện có sang JobRunDto, không đổi fields. `pollJobUntilTerminal({runId,signal,getStatus,intervalMs=600,maxAttempts=200}): Promise<JobRunDto>`; getStatus nhận `(runId: string, signal: AbortSignal)`.

- [ ] Fake-timer RED cases Queued→Running→Succeeded, Failed, abort trước fetch/trong delay/trong request, fetch error, đúng 200 non-terminal responses rồi typed timeout. Assert timer count=0 và không fetch tiếp sau settle.
- [ ] Helper dừng khi terminal, abort hoặc fetch error, không ngầm retry transient error; tháo abort listener sau mỗi delay. Pass cùng signal tới getJson.
- [ ] Job API gọi `/admin/jobs/{runId}` theo base path httpClient hiện có; token lấy theo pattern hiện tại. Hai page disable trigger tới terminal; success reload results, Failed show sanitized error, error/timeout show retry, cleanup abort khi unmount hoặc đổi branch.
- [ ] Component tests chứng minh reload tự động và old request không cập nhật state của branch mới. Giữ DTO compatibility cho consumers còn dùng ForecastJobRunDto bằng type alias nếu cần.

```powershell
npm --prefix frontend test -- --run
npm --prefix frontend run build
dotnet test OnlineSupermarket.slnx --no-restore --filter "FullyQualifiedName~BranchInventoryTests|FullyQualifiedName~InventoryMutation|FullyQualifiedName~ReviewEndpointsTests|FullyQualifiedName~RecommendationEndpointsTests|FullyQualifiedName~DemandForecastTests"
```

**Tự review:** không timer/request leak; hai page hiển thị terminal thật. **Commit:** `fix(frontend): share bounded job polling`.

## R13 — Restore immutable history và cleanup (Task gốc 13)

**Files:** lịch sử trong `backend/src/OnlineSupermarket.Infrastructure/Persistence/Migrations/` đến AddBackgroundJobRunBranch; `.gitignore`; `backend/src/OnlineSupermarket.Api/OnlineSupermarket.Api.csproj`; `backend/src/OnlineSupermarket.Infrastructure/Persistence/{AppDbContextFactory,RuntimeDbContextFactory}.cs`. Debris targets theo plan gốc.

**Dependency:** R1–R12 model review kết thúc; cần inventory migrations đã deploy từ release operations. Không đoán production state.

- [ ] Ghi SHA baseline và so sánh từng historical .cs/.Designer.cs với `git show deeccfd:<path>`. Lập danh sách nội dung cần phục hồi bằng patch; không lấy snapshot đã sửa làm immutable source.
- [ ] Thu bằng chứng mọi environment cho hai ID SyncModelAndMigrations/RestoreRecommendationConstraints bằng query plan gốc. Nếu deployed, giữ identity migration và tạo migration sau nó; nếu chưa xác minh, chưa được xóa.
- [ ] Sau khi xác minh, restore historical files chính xác baseline và bỏ repair migration chưa deploy. Từng path delete phải resolved dưới repository, đã kiểm tra untracked và không có source người dùng cần giữ; ưu tiên backup recoverable trong thư mục tạm xác định rõ.
- [ ] Cleanup đúng `/artifacts/`, `/backend/src/OnlineSupermarket.Api/test-console/`, provider scratch; thêm ignore chính xác. Không xóa `backend/scripts/`, Fixtures hoặc docs chỉ vì untracked.
- [ ] Bỏ csproj exclusions chỉ dành debris; official factory đã đăng ký thì loại RuntimeDbContextFactory không dùng. Simplify design-time factory giữ khả năng script không kết nối DB, test tương ứng phải cập nhật cùng task.

```powershell
git diff deeccfd -- backend/src/OnlineSupermarket.Infrastructure/Persistence/Migrations/20260807082824_InitialFoundation.cs
dotnet build OnlineSupermarket.slnx --no-restore
git diff --check
```

**Tự review:** audit tất cả historical files bằng hash, không chỉ ví dụ một file; chưa sinh final migration; không mất source/data. **Commit:** `chore(migrations): restore immutable history and remove debris`.

## R14 — Forward convergence migration (Task gốc 14)

**Files:** tạo migration `NormalizeBackgroundJobSchemaAndRecommendationConstraints` và designer trong Persistence/Migrations (timestamp `20260906090000` chỉ khi chưa có ID mới hơn bắt buộc); snapshot; BackgroundJobRunConfiguration.cs/RecommendationResultConfiguration.cs; `backend/tests/OnlineSupermarket.Infrastructure.Tests/Persistence/MySqlMigrationUpgradeTests.cs`.

- [ ] RED MySQL cases fresh, data-bearing committed baseline, PascalCase, snake_case, missing/valid/invalid recommendation constraints, partially normalized index/columns. DB riêng mỗi case.
- [ ] Generate từ final model, inspect snapshot. Migration thực hiện preflight tất cả object trước DDL: information_schema source/target từng column, ambiguous both/missing both phải SIGNAL diagnostic; validate rank/score trước add CHECK.
- [ ] Rename an toàn từng column, normalize unique lock index không mở khoảng trống uniqueness; idempotent với schema đã đúng. Backfill branch_id chỉ `branch:{valid-guid}` có branch hợp lệ; malformed/global để null, không đoán branch.
- [ ] Xử lý partial DDL có thể retry hội tụ ở trạng thái an toàn; ambiguity phải fail trước mutation. Không DROP table/data để chữa schema.
- [ ] Down throw NotSupportedException giải thích backup restore do không phục hồi casing/constraint provenance.

```powershell
dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests --filter FullyQualifiedName~MySqlMigrationUpgradeTests
dotnet ef migrations has-pending-model-changes --project backend/src/OnlineSupermarket.Infrastructure --startup-project backend/src/OnlineSupermarket.Api
dotnet ef migrations script --project backend/src/OnlineSupermarket.Infrastructure --startup-project backend/src/OnlineSupermarket.Api
```

**Tự review:** generated ID đúng deployed history; no pending model; fresh/legacy cùng schema, zero data loss. **Commit:** `fix(migrations): converge supported schemas forward`.

## R15 — Upgrade/recovery proof và runbook (Task gốc 15)

**Files:** sửa `backend/tests/OnlineSupermarket.Infrastructure.Tests/Persistence/{MySqlMigrationUpgradeTests,MySqlSchemaTests}.cs`, `docs/mysql-upgrade.md`, `docs/superpowers/plans/2026-09-05-mysql-upgrade-repair.md`.

- [ ] Fresh migrate tới latest: assert tables, columns, unique indexes, FKs, utf8mb4, enforced recommendation checks, HasPendingModelChanges=false.
- [ ] Seed baseline gồm branches, queued/running/terminal jobs, ledger, views, recommendations, forecasts; compare IDs/counts/values/timestamps/token trước/sau, chỉ cho phép backfill đã ghi rõ.
- [ ] Partial cases: một constraint có sẵn, partial index, normalized columns; safe state converge, ambiguous state dừng không mutate. Lưu failure diagnostic đã sanitize.
- [ ] Runbook ghi command backup/restore rehearsal trên database disposable, restore checksum/count verification, compatibility decision matrix. Trước migration failure chỉ rollback binary nếu xác nhận DB chưa đổi; sau DDL incompatible phải stop writes, capture schema/history, restore verified backup rồi binary cũ. Không dựa EF Down hoặc MySQL transaction rollback.
- [ ] Bỏ mandatory pre-script, thêm superseded notice cho plan cũ. Database.MigrateAsync tự hoạt động từ mọi supported state; compatibility test chưa có thì không ghi binary rollback an toàn.

```powershell
dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests --filter "FullyQualifiedName~MySqlMigrationUpgradeTests|FullyQualifiedName~MySqlSchemaTests"
dotnet ef migrations has-pending-model-changes --project backend/src/OnlineSupermarket.Infrastructure --startup-project backend/src/OnlineSupermarket.Api
git diff --check
```

**Tự review:** có rehearsal restore thật, không chỉ văn bản; immutable hashes còn đúng. **Commit:** `test(migrations): verify upgrades and recovery runbook`.

## R16 — Final integration và requirement audit (Task gốc 16)

**Files:** tạo `compose.e2e.yaml`, `frontend/src/test/run-remediation-e2e-tests.mjs`, `backend/tests/OnlineSupermarket.Infrastructure.Tests/Persistence/MySqlRemediationFlowTests.cs`; sửa `frontend/package.json`, `docs/mysql-upgrade.md`; cập nhật checklist plan gốc và tài liệu này.

- [ ] Compose project `online-supermarket-e2e`, ports riêng, MySQL 8.4, disposable named volume, background services enabled, secrets test runtime; runner có try/finally teardown đúng project+volume.
- [ ] Browser flow đăng nhập → cart/order → non-COD payment → callback success ký đúng gửi tuần tự/trùng/concurrent → terminal UI; complete order bằng authorized API/UI → một Sale ledger đúng snapshots. Flow failure → một Release, promotion restored, availability không âm.
- [ ] Admin flow record views valid branch → trigger recommendations/forecast → quan sát active rồi terminal → results tự reload. Invalid branch trả controlled 404. Không dùng fake network terminal response để chứng minh worker.
- [ ] Restart flow seed Queued bằng test harness DB helper, stop API trước consume, restart và assert same runId chạy đúng một lần; expired Running recovery vs renewal race không ghi đè winner. Không thêm public test backdoor vào production API.
- [ ] DB assertions concurrent trigger cùng lock, callback duplicates, một terminal transition, không duplicate recommendation/forecast output của winning run. Các helper seed/assert nằm trong test project, runner gọi lệnh tường minh.
- [ ] Chạy clean-process gates dưới đây; ghi kết quả test totals và teardown evidence. Nếu fail, sửa đúng task liên quan rồi chạy lại gate chịu ảnh hưởng và final gate.

```powershell
dotnet build OnlineSupermarket.slnx --no-restore
dotnet test OnlineSupermarket.slnx --no-restore
npm --prefix frontend test -- --run
npm --prefix frontend run build
npm --prefix frontend run test:e2e:remediation
git diff --check
```

- [ ] Map mọi finding trong release checklist plan gốc sang tên regression test cụ thể, commit, kết quả; scan logs/payload/ErrorSummary theo seeded secret markers; xác nhận không orphan process/container/timer/volume và không historical migration diff.
- [ ] Chỉ khi tất cả đạt mới đánh dấu 16 task gốc hoàn tất và ghi release-candidate evidence. Nếu environment/deployed inventory còn thiếu, ghi chính xác gate chưa chạy và không tuyên bố release-ready.

**Tự review:** hành vi payment→inventory→jobs→API/UI thật, restart thật, database constraints thật. **Commit:** `test: complete remediation integration gate`.

## Quy tắc báo cáo sau mỗi task

Ghi ngay trong task đã thực hiện: file thay đổi, tên regression tests, lệnh/exit code/pass-fail, finding tự review và cách sửa, commit hash. Không đánh dấu task tiếp theo khi task hiện tại chưa đạt. Không dừng vì task lớn; chỉ dừng phần phụ thuộc khi có blocker cần thông tin/quyền truy cập thật từ người dùng.

## Kết quả tự review kế hoạch

- Bao phủ toàn bộ Tasks 1–16; đã mở lại Tasks 1–3, 8–11 còn thiếu bằng chứng hoặc implementation.
- Giữ API envelope/JSON, snake_case schema, MySQL thật, immutable history, forward-only recovery và sequential execution.
- Sửa tên test frontend theo file thực tế `ProductDetail.test.tsx`; không tham chiếu nhầm ProductDetailPage.test.tsx.
- Không gộp thay đổi migration có sẵn vào commit chức năng; deployed inventory là prerequisite thật cho removal, không tự suy diễn là chưa deploy.
- Plan này được lập từ audit code và Git, không phải báo cáo full tests pass hoặc xác nhận release an toàn.
