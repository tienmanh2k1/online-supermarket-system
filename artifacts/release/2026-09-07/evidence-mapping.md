# 07/09 — Task → Evidence Mapping (review follow-up)

Ngày tạo: 2026-09-07. Repo HEAD: `85f48aee73d89e6f3e7fd62a1f31b2499c29ba4a` (không commit; changes dưới working tree).

Quy ước: mỗi mục ghi (a) task trên board, (b) cách kiểm chứng, (c) đường dẫn evidence/tests thực.

## A1 — Baseline & Git
- Evidence: `artifacts/release/2026-09-07/baseline/` (git-baseline.txt, head.txt, head-log.txt, git-status-short.txt, scope.md).
- Scope: FR registry cập nhật `docs/requirements/functional-requirements.md` (header + mục "Scope Release Rút Gọn").
- **Diff/hash manifest:** thay đổi ngày 07 gồm các file production + tests + ml-poc. Manifest: `artifacts/release/2026-09-07/manifest-diff-day07.txt` (bảng mã băm SHA-256 chi tiết cho từng file modified/untracked) kèm git diff patch `artifacts/release/2026-09-07/working-tree-day07.patch`. Đồng bộ mã nguồn sang `../clean-install-test` đạt khớp mã băm 100%.

## A2 — Clean Install
- Cài mới cô lập: nguồn copy `../clean-install-test` (20MB, bỏ bin/obj/node_modules), project `clean-test`, DB `online_supermarket_test` (riêng), volume `clean-test_mysql_data` (riêng).
- Đồng bộ toàn diện: Tất cả các file source (`JobErrorSanitizer.cs`, `RecurringJobScheduler.cs`, `MySqlJobRunLifecycleConcurrencyTests.cs`) khớp SHA-256 100%.
- Rebuild & startup sau lần đồng bộ cuối: `artifacts/release/2026-09-07/clean-install/rebuild-startup-log.txt` (1238 dòng log API khởi động, migrate 23 bảng, seed dữ liệu, recurring jobs chạy).
- Ports và cô lập (F2/R2): `compose.test.yaml` dùng `!override`; effective config `effective-config.txt` (3 published ports: 8087/5178/33067, không có 8080/5173/3306); `compose-ps-isolated.txt` ghi nhận `Up 48 seconds`, chỉ bind 3 cổng test.
- Health 200: `health-after-isolated-up.txt` (HTTP 200 lúc 13:25:07, `{"status":"ok"}`); Frontend HTTP 200 trên cổng 5178.
- Teardown: `teardown.txt` (`down -v`, exit 0, containers/network/volume xoá sạch, `docker compose -p clean-test ps` rỗng).

## A3 — Blocker Fix & Smoke Core
- Jobs:
  - Worker bị tắt (Development): fix `compose.yaml` override `Infrastructure__DisableBackgroundServices=false`. Chứng minh: run Forecast/Recommendation Succeeded (`jobs.txt`, live).
  - Recovery mồ côi: `RecurringJobScheduler` gọi `RecoverStaleJobsAsync` đầu vòng; live chứng minh `restart-recovery-evidence.txt` (orphan run `08262c4f...` inserted khi api down → sau restart → Succeeded).
  - **JobRunPublishGuard & Lease Concurrency (R3 - hoàn tất triệt để):**
    - Sửa `MySqlJobRunLifecycleConcurrencyTests.PublishGuard_WhenLeaseLostOrExpired_AbortsPublishOnMySql`:
      1. Seed `Running` với token hợp lệ (`token-fail`, `token-succeed`) và lease tương lai.
      2. Chuyển đổi trạng thái qua store `TryFailAsync` và `TrySucceedAsync`, assert trả về `true`.
      3. Truy vấn trực tiếp DB context kiểm tra trạng thái thực tế đã là `JobRunStatus.Failed` và `JobRunStatus.Succeeded`.
      4. Kiểm tra guard `JobRunPublishGuard.EnsureOwnedAsync` từ chối riêng biệt cả 2 trạng thái terminal và trạng thái hết hạn (`OperationCanceledException`).
      5. Thao tác ghi batch kết quả (`RecommendationResults`) trong transaction bị rollback khi guard ném ngoại lệ; assert xác nhận 0 dòng kết quả được ghi vào DB cho các run expired/failed/succeeded (và 1 dòng được ghi thành công cho live unexpired run).
    - Kết quả chạy test: 5/5 concurrency tests pass (18s trên MySQL Testcontainers).
- **Sanitizer (fix F1 & R1, R4):**
  - `JobErrorSanitizer` regex hỗ trợ escape sequence `"(?:\\.|[^"\\])*"` và `'(?:\\.|[^'\\])*'` cùng quote đóng trước `:` (`"password":"x"`, `'secret':'y'`, `{"password":"prefix\"AUDIT_MARKER"}`).
  - Test mới: `JobErrorSanitizerTests` (12 tests) bao gồm escaped quote/backslash và quoted keys. 12/12 pass.
  - `JobRunExecutor` đã sanitize cả log + ErrorSummary (`JobRunExecutor.cs:63-65`); test `ThrowingHandler_RedactsSyntheticSecretMarker_FromErrorSummaryAndLogs` xác nhận cả ErrorSummary và logger không rò rỉ synthetic marker kể cả khi có escaped quotes.
  - `RecurringJobScheduler` sửa log raw exception sang sanitized `JobErrorSanitizer.Sanitize(ex)`; test `RecurringJobSchedulerTests.Scheduler_IterationException_RedactsSyntheticSecretMarkerInLog` dùng `TaskCompletionSource` đồng bộ tín hiệu và dọn dẹp sạch tài nguyên (R4), xác nhận log không chứa synthetic marker.
  - Kết quả: Đóng dứt điểm F1, R1, R3, R4. TRX xác nhận: `release-infra-r3-final.trx` (229/229 pass, 0 fail, 0 skip).
- Auth boundary: `core-smoke-summary.txt` (Customer→admin: 403/403/405, no-token: 401) — ghi lại từ curl live.
- Checkout/payment/stock:
  - `checkout = HTTP 201` (order `1911a7af`), COD PendingCollection, order→Completed full transitions 200 (ghi `core-smoke-summary.txt`).
  - Ledger: 3×Reserve(+1) → 3×Sale (on_hand −1, reserved −1 → 0) đúng; không âm, không duplicate effects (curl + MySQL live).
  - Regression MySQL sẵn có: `MySqlInventoryTransactionTests` (rollback/duplicate/operation-key), `MySqlJobRunLifecycleConcurrencyTests` (lease/concurrency + publish guard), `PaymentCallbackProcessor/Verifier` tests. Tên test maps trong `release-infra-r3-final.trx`.
- Logs: `api-startup-log.txt` không có secret marker; `git-baseline` note pre-existing `git diff --check` whitespace ở 13 Designer (phiên trước), chưa sửa (D1/deadline day).

## B1 — ML POC
- Package `Microsoft.ML.Recommender 0.22.0`, net10.0 patch.
- Fixture 5×8×30 interactions, seed 42 → train ~1.0-1.2s, **6/6 probes finite**, exit 0 nhiều lần chạy (report `poc-results.txt`, `poc-rerun-after-f4.txt`).
- **fix F4:** assertion `probes.Length>=5 && finite==probes.Length`; comment user3 không phải cold-start; evidence source `poc-fixture-Program.cs` (updated sau fix).
- Checkpoint 3 files (csproj/scorer/handler) trước POC tại `ml-poc/checkpoint/`.
- B2 CF: không áp dụng (MF pass). Ghi rõ không triển khai.

## A4 — Docs/README
- `submission/I_Working_Application/README.md`: ports 8080/5173, accounts `admin@test.com`/`Test@123`, thay "184 test" → baseline 967.
- `submission/README.md`: ports + accounts đúng.
- Report master: giữ nguyên khung, tên SV giả + TODO-D10 comment (quyết định chủ dự án).

## Baseline test-run (khi phương pháp)
- Domain 171 + API 246 + Infra 229 = 646 backend, + Vitest 330 = 976 tests pass. Evidence:
  - Backend: `release-domain_*.trx` (171), `release-api_*.trx` (246), `release-infra-r3-final.trx` (229).
  - Frontend: `vitest-2026-09-07.log` (45 files, 330 pass).

## Blocker / mở lại
- Toàn bộ các findings gốc F1–F4 và các điểm review mở lại (R1, R2, R3, R4) đã được giải quyết trọn vẹn kèm evidence và test tự động.
- Gate A chính thức ĐẠT (PASSED) đầy đủ tiêu chí. Sẵn sàng tiến hành Day 08/09 (Gate B: AI Thật - Nối MF vào RecommendationJobHandler).
- Tên SV report hoãn D10 (đã ghi TODO-D10). `git diff --check` whitespace phiên trước trì dừng RC (D1).

## Files thay đổi production ngày 07 (working tree, chưa commit)
`compose.yaml`, `DependencyInjection.cs`, `ForecastJobHandler.cs`, `InventoryMutationService.cs`, `CheckoutEndpoints.cs`, `OrderEndpoints.cs`, `ReviewEndpoints.cs`, `RecommendationEndpoints.cs`, `PaymentCallbackProcessor.cs`, `JobRunPublishGuard.cs`, `RecurringJobScheduler.cs`, `JobErrorSanitizer.cs`, `OnlineSupermarket.Api.csproj` (exclude ml-poc), các test đã nói. (Danh sách đầy đủ: git-baseline.txt)