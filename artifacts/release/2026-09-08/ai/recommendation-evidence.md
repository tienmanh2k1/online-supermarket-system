# AI Integration & Recommendation Evidence - Gate B

**Date:** 2026-09-08  
**Release Gate:** Gate B — AI & Recommendation Engine Verification  
**Git HEAD:** `85f48aee73d89e6f3e7fd62a1f31b2499c29ba4a`  
**Status:** PASS

---

## 1. Environment & Stack Baseline

- **Backend API:** `http://localhost:8080` (Health endpoint: `http://localhost:8080/api/health` → `200 OK`)
- **Frontend Client:** `http://localhost:5173` (Nginx/Vite bundle)
- **Database:** MySQL 8.4 (`localhost:3306`, Database: `OnlineSupermarket`)
- **Tech Stack:** .NET 10, Microsoft.ML.Recommender 0.22.0, React 19 / TypeScript / Vite, MySQL, xUnit, Vitest
- **Baseline Snapshot:** [baseline.txt](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/baseline.txt)
- **Database Pre-Seed Backup:** [backup_before_seed.sql](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/backup_before_seed.sql)

---

## 2. Training Dataset & Fixture (B4 / Task 031)

- **Seed Script:** `scripts/seed-gate-b-demo.mjs` (idempotent, preflight check before adding view signals)
- **Dataset Artifact:** [dataset.json](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/dataset.json)
- **Signal Window:** 28 days (`viewed_at_utc >= DATE_SUB(UTC_TIMESTAMP(), INTERVAL 28 DAY)`)
- **Interaction Data Counts:**
  - Raw product view events in window: **35**
  - Completed order items in window: **3**
  - Distinct interaction signals input to `MfScorer`: **31** (exceeds the threshold of ≥5 signals)
  - Demo Users: 5 active users with interactions (`user1` to `user5`), 1 clean cold-start user (`colduser@test.com`), 1 dedicated merge user (`mergeuser@test.com`)
  - Target Products: 8 active products with active brands and confirmed available stock (quantity 30) at branch `de4870ff-651e-4eb5-b509-21a834d76bb2` (AptechMart Quận 1)
- **Unseen Candidates for User 1 (U1):**
  - U1 interacted with P1..P6 (MacBook Air, Galaxy S24 Ultra, iPhone 15 Pro Max, iPhone 15, AirPods Pro 2, MacBook Pro 14).
  - Candidates P7 (`Dell XPS 15 9530`) and P8 (`Samsung Galaxy Tab S9 FE`) are completely unseen by U1 (0 views, 0 purchases).
  - P7 and P8 were interacted with by other users in the training set (P7 by U2, U3, U4, U5; P8 by U3, U4, U5), ensuring the matrix factorization model learns latent factors for them.

> [!NOTE]
> Đạt điều kiện thử train (≥5 tín hiệu); khả năng sinh gợi ý cá nhân hóa còn phụ thuộc vào mapping GUID sang uint, điểm score dự đoán dương, và các ứng viên unseen còn hàng/active tại chi nhánh được chọn. Ma trận 30 cặp tương tác là dữ liệu demo tổng hợp phục vụ nghiệm thu ranh giới kỹ thuật, không dùng để công bố chất lượng model thực tế.

---

## 3. Automated Test Suites & Regression Verification (B3)

Targeted test execution results across all architectural layers:

### A. Infrastructure Regression & Failure Tests (xUnit)
- **Command:** `dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests --no-restore --filter "FullyQualifiedName~MfScorerTests|FullyQualifiedName~RecommendationJobHandlerTests|FullyQualifiedName~RecommendationScorerTests"`
- **Result:** **24 passed / 24 total** (0 failed, 0 skipped)
- **Log TRX:** [gate-b-recommendations.trx](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/tests/gate-b-recommendations.trx)
- **Key Verifications:**
  1. `EnsureModel_WithSufficientData_TrainsModelAndProducesPositiveMfScores`: Model trains, scores positive and bounded in [0, 1], produces user recommendations with `mf-v1`.
  2. `EnsureModel_WithBelowThresholdInteractions_DoesNotTrainModel_AndUsesContentFallback`: Below 5 signals, does not train MF, falls back to `content-v1`.
  3. `Score_UnknownUserOrProduct_ReturnsZero`: Unknown user/product safely returns `0m`.
  4. `ScoreUserWithMf_ExcludesSeenViewsAndPurchases`: Excludes products already viewed or purchased.
  5. `ScoreUserWithMf_WhenAllCandidatesSeen_DoesNotReinsertSeen`: No duplicate insertion of seen items.
  6. `ExecuteAsync_WhenTrainingFails_CleansUpModelAndThrows_PreservingPriorBatch`: Trainer exception triggers `MfScorer.ClearModel()`, previous batch rows remain completely intact, 0 rows saved for failed run.
  7. `ExecuteAsync_WhenPredictionYieldsNoScores_FallsBackToContentWithoutFakingMf`: Prediction failure gracefully falls back to `content-v1` with proper reason without faking `mf-v1`.
  8. `ExecuteAsync_WithConflict_RollsBackTheWholeBatch`: Publish transaction conflict rolls back the entire batch.

### B. API Endpoints & Permission Tests (xUnit)
- **Command:** `dotnet test backend/tests/OnlineSupermarket.Api.Tests --no-build --filter "FullyQualifiedName~Recommendation"`
- **Result:** **20 passed / 20 total** (0 failed, 0 skipped)
- **Coverage:** Guest view recording, authenticated view recording, session merge, public recommendations with branch filtering, admin batch results.

### C. Frontend Unit & Component Tests (Vitest)
- **Command:** `node node_modules/vitest/vitest.mjs run src/features/recommendations/recommendationSession.test.ts src/features/recommendations/RecommendationShelf.test.tsx src/features/admin/AdminRecommendationsPage.test.tsx src/features/auth/AuthContext.test.tsx`
- **Result:** **18 passed / 18 total** (4 test files passed)
- **Coverage:** Anonymous session GUID lifecycle, loading skeletons, empty state hiding, error state & retry re-triggering, branch switching, admin metadata display, login merge dispatch.

---

## 4. Live MF Batch Execution & Verification (B5 / B7)

A live batch run was triggered, executed, and validated end-to-end after an API container restart:

- **Batch Job Run ID:** `daca78f9-1d6a-4813-b41b-cd81b1e60925`
- **Run Record:** [live-run.json](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/live-run.json)
- **Status:** `Succeeded`
- **Execution Duration:** ~2 seconds

### Scope & Algorithm Version Breakdown in MySQL
Database queries directly verified the generated batch rows:

| Scope | Algorithm Version | Row Count | Reason |
|---|:---:|:---:|---|
| **Global** | `content-v1` | 9 | `Sản phẩm nổi bật toàn hệ thống` / `Được nhiều người quan tâm` |
| **SimilarProduct** | `content-v1` | 64 | `Cùng danh mục và thương hiệu` |
| **User** | `mf-v1` | **14** | `Gợi ý từ mô hình AI` |

Full database record dump: [db-verification.txt](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/db-verification.txt).

### Admin Results Sample
- **Endpoint:** `GET /api/admin/recommendations/results?scope=User&limit=50`
- **Response Record:** [admin-sample.json](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/admin-sample.json)
- **Algorithm Version:** `mf-v1`
- **Reason:** `Gợi ý từ mô hình AI`
- **Top User Recommendations Sample:**
  - Rank 1: Product `0baae464-4f4c-49a0-8699-743aa32e28b3` (Dell XPS 15), Score: `0.8742`
  - Rank 2: Product `3201f989-ff03-4a97-be7c-620ea7bc1b32` (Samsung Galaxy Tab S9 FE), Score: `0.5790`
  - Rank 3: Product `bd00c190-a496-484f-8818-98dba5a52533` (LG Door-in-Door 601L), Score: `0.1783`

### Customer Personalized Shelf (User 1)
- **User:** `user1@test.com` (Nguyen Van An)
- **Endpoint:** `GET /api/recommendations?branchId=de4870ff-651e-4eb5-b509-21a834d76bb2&limit=20`
- **Response Record:** [personalized-response.json](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/personalized-response.json)
- **Source Scope:** `User`
- **Items Returned:**
  1. Dell XPS 15 9530 (Score: 0.8742, Reason: *Gợi ý từ mô hình AI*, In Stock: 30)
  2. Samsung Galaxy Tab S9 FE (Score: 0.5790, Reason: *Gợi ý từ mô hình AI*, In Stock: 30)
  3. LG Door-in-Door 601L InstaView (Score: 0.1783, Reason: *Gợi ý từ mô hình AI*, In Stock: 30)
- **Overlap with Seen Products:** **0** (strictly excludes P1..P6).

### Cold-Start Shelf (Clean User)
- **User:** `colduser@test.com` (Cold Start User, 0 views, 0 purchases)
- **Endpoint:** `GET /api/recommendations?branchId=de4870ff-651e-4eb5-b509-21a834d76bb2&limit=20`
- **Response Record:** [cold-start-response.json](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/cold-start-response.json)
- **Source Scope:** `Global` (Fallback)
- **Top Item:** MacBook Air 15" M3 (Score: 0.9200, Reason: *Được nhiều người quan tâm*)
- **AI Reason Present:** **None** (Cold start users strictly receive content/popularity fallback without faking AI labels).
- **Differentiation:** U1 and Cold user receive completely distinct recommendations and reason labels.

---

## 5. Anonymous Session Merge Verification (B7 / Task 043)

- **Verification Script:** `scripts/verify-gate-b-merge.cjs`
- **Evidence Record:** [merge-verification.json](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/merge-verification.json)
- **Report:** [ui-checks.md](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/ui-checks.md)
- **Verified Steps:**
  1. Anonymous guest viewed product `0baae464-...` (Dell XPS 15) with session `00c202c6-...`. Event recorded with `user_id = NULL`.
  2. User logged in through the browser UI form (`LoginForm.tsx`).
  3. `POST /api/recommendations/session/merge` intercepted, returning HTTP 200 `{ "mergedCount": 1 }`.
  4. Database row verified updated to `user_id = 63cd1842-1e8c-4a34-9dae-170b624c316a` (`mergeuser@test.com`).
  5. Re-merging the same session returned `{ "mergedCount": 0 }` (no duplicates).
  6. Unauthenticated merge returned HTTP 401 Unauthorized.
  7. Cross-account merge attempt by `user2@test.com` returned `{ "mergedCount": 0 }` (0 ownership leakage).
  8. Merged product immediately appeared in `seenProducts` of `mergeuser`, correctly excluding it from candidate recommendations.

---

## 6. Restartability & Model Lifecycle (Task 034)

- Model training occurs in-memory via `Microsoft.ML.Recommender`.
- Container restart test performed: `docker restart online-supermarket-api-1`.
- API restarted cleanly (Health 200 OK).
- Retrain triggered via `POST /api/admin/jobs/recommendations/runs` successfully completed in 2s (Run `daca78f9-1d6a-4813-b41b-cd81b1e60925`), producing identical valid recommendations in database and API.
- Re-training on demand replaces the need for serialized model binary files on disk for the current deployment architecture.

---

## 7. Quality Metrics Status (Task 033 / 036 / 037)

- **Status:** **DEFERRED** (Hoãn đánh giá chất lượng model trên holdout độc lập).
- **Rationale:** Dataset hiện tại là tập dữ liệu demo tổng hợp có kiểm soát (31 signals, 5 active users, 8 products) nhằm mục đích kiểm chứng tích hợp kiến trúc, mapping ID, lọc seen/branch, và fallback logic. Chưa có tập holdout độc lập đủ lớn và mang tính đại diện để tính toán các chỉ số thống kê (như RMSE hay NDCG) có ý nghĩa thực tế.
- **Proposed Metric for Gate D / Future Work:** `Precision@5` hoặc `HitRate@5` khi có tập log tương tác thực tế từ người dùng.
- **Commitment:** Không công bố con số chất lượng chưa đo đạc; không gọi tỷ lệ score dương (14/14) là độ chính xác (accuracy).

---

## 8. Captured Visual Evidence

1. **Admin Model & User Scope Results:**  
   `artifacts/release/2026-09-08/ai/admin-model.png`  
   Shows batch run `daca78f9-...`, `AlgorithmVersion = mf-v1`, `Scope = User`, generated timestamp, and positive scores with reason "Gợi ý từ mô hình AI".

2. **Personalized Shelf (User 1):**  
   `artifacts/release/2026-09-08/ai/personalized.png`  
   Shows logged in user `Nguyen Van An`, selected branch AptechMart Quận 1, recommendation shelf with unseen candidates (Dell XPS 15, Galaxy Tab S9 FE, LG Door-in-Door) bearing the label "Gợi ý từ mô hình AI".

3. **Cold Start Fallback Shelf:**  
   `artifacts/release/2026-09-08/ai/cold-start.png`  
   Shows logged in user `Cold Start User`, selected branch AptechMart Quận 1, recommendation shelf displaying popularity/global fallback items without AI labels.

4. **Product Detail Page (PDP) Similar Products Shelf:**  
   `artifacts/release/2026-09-08/ai/pdp-similar.png`  
   Shows PDP for MacBook Air 15" M3 (`/product/37059735-...`), displaying the "Sản phẩm tương tự" recommendation shelf with 8 similar items (MacBook Pro 14", Dell XPS 15, ASUS ZenBook, ASUS ROG, AirPods Pro 2, etc.) and branch inventory availability verified.

---

## 9. Review Findings Resolution (S1 — S4, R5 — R8)

| Finding | Phân Loại | Biện Pháp Khắc Phục & Bằng Chứng | Trạng Thái |
|---|:---:|---|:---:|
| **S1: Admin sample API contract limit=50 & Plan sync** | P2 | Đồng bộ `limit=50` trong [2026-09-08-gate-b-completion-plan.md](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/docs/superpowers/plans/2026-09-08-gate-b-completion-plan.md#L108) thay vì `limit=100`. Kiểm chứng chi tiết từng row trong DB theo `runId` và `audienceKey = "user:{userId}"`. Xác nhận 14/14 User rows đều mang `AlgorithmVersion = "mf-v1"`. | **CLOSED** |
| **S2: Bổ sung PDP Similar→Global, Expiry & Failed Batch Protection** | P2 | Bổ sung 4 test cases trong [RecommendationReadEndpointsTests.cs](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/backend/tests/OnlineSupermarket.Api.Tests/Endpoints/RecommendationReadEndpointsTests.cs): (1) `ProductRecommendations_WhenSimilarMissingOrExpired_FallsBackToGlobalWith200`, (2) `ProductRecommendations_WhenSimilarExpired_AndGlobalValid_FallsBackToGlobalWith200`, (3) `ProductRecommendations_WhenAllRowsExpired_ReturnsEmpty200`, (4) `Homepage_WhenNewRunFailed_ServesPriorSucceededBatchWith200`. Thực hiện browser smoke test tự động qua Playwright [scripts/verify-pdp-smoke.cjs](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/scripts/verify-pdp-smoke.cjs), xuất [pdp-smoke.json](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/pdp-smoke.json) và ảnh [pdp-similar.png](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/pdp-similar.png). | **CLOSED** |
| **S3: Nghiệm thu quyền, Conflict & UI Terminal Failed State** | P2 | Bổ sung test `TriggerRun_AsCustomer_Returns403` trong [AdminRecommendationEndpointsTests.cs](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/backend/tests/OnlineSupermarket.Api.Tests/Endpoints/AdminRecommendationEndpointsTests.cs). Frontend: thêm hàm `pollUntilTerminal` polling trạng thái run đến `Succeeded` hoặc `Failed`, unblock UI (`running: false`), hiển thị thông báo lỗi. Bổ sung test cases trong [AdminRecommendationsPage.test.tsx](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/frontend/src/features/admin/AdminRecommendationsPage.test.tsx) kiểm tra cả 2 nhánh Succeeded và Failed. (14/14 vitest pass). | **CLOSED** |
| **S4 / R5: Seed DB Preflight fail-fast & Target Configurable** | P2 | Sửa [scripts/seed-gate-b-demo.mjs](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/scripts/seed-gate-b-demo.mjs): `queryDb` quăng lỗi ngay lập tức `[DB_FATAL]` thay vì catch trả chuỗi rỗng; cấu hình rõ biến môi trường DB target (`DB_CONTAINER`, `DB_NAME`, `DB_USER`, `DB_PASSWORD`); bổ sung preflight `verifyEnvironmentConsistency` kiểm tra kết nối DB, API `/api/health` và tồn tại branch trước mọi thao tác mutation. Không chạy seed trong review để bảo toàn dữ liệu demo. | **CLOSED** |
| **R6: Live Verifier Assertions & Correlation Hardening** | P2 | Sửa [scripts/verify-gate-b-live.mjs](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/scripts/verify-gate-b-live.mjs): (1) Assert nghiêm ngặt `adminSample.jobRunId === runId` vừa trigger; (2) Assert fixture Cold phải nhận `sourceScope === 'Global'` và danh sách items không được rỗng (`items.length > 0`), cấm hoàn toàn lý do AI; (3) Đối chiếu product IDs trong response public của cả U1 và Cold trực tiếp với persisted DB rows của đúng `runId` đó; (4) Assert fail-fast nếu U1 và Cold nhận cùng top item hoặc cùng reason. | **CLOSED** |
| **R7: MF Prediction Failure Boundary Seam & Tests** | P2 | Thêm injection seam `MfScorer.ScoreSeam` tại ranh giới tính điểm prediction của model đã train (reset an toàn trong `ClearModel`). Bổ sung test `ExecuteAsync_WhenPredictionFailsOnTrainedModel_FallsBackToContentWithoutFakingMf` trong [RecommendationJobHandlerTests.cs](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/backend/tests/OnlineSupermarket.Infrastructure.Tests/Recommendations/RecommendationJobHandlerTests.cs) huấn luyện model thật (15 tương tác, 5 users, 8 items), inject lỗi tại prediction boundary, xác nhận model `IsModelTrained == true`, handler fallback thành công về `content-v1` ("Phù hợp danh mục đã xem") và không phát hành `mf-v1`. Bổ sung test `ExecuteAsync_ViaJobRunExecutor_WhenTrainingFails_MarksRunFailedInStore` chứng minh `JobRunExecutor` bắt exception từ handler và chuyển trạng thái run thành `JobRunStatus.Failed` trong store. Trong [MfScorerTests.cs](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/backend/tests/OnlineSupermarket.Infrastructure.Tests/Recommendations/MfScorerTests.cs), test `AllCandidatesSeen` đổi sang assert `Assert.Empty(results)`. | **CLOSED** |
| **R8: Candidate Availability Query thực tế trong Seed** | P2 | Sửa [scripts/seed-gate-b-demo.mjs](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/scripts/seed-gate-b-demo.mjs): truy vấn trực tiếp trạng thái thực tế từ MySQL (`p.is_active = 1`, `b.is_active = 1`, `bi.available_quantity > 0`) tại branch demo cho ứng viên P7 và P8; fail-fast với `[AVAILABILITY_FATAL]` nếu ứng viên không hợp lệ; xuất thông tin thực tế vào `dataset.json` thay vì gán hằng số. | **CLOSED** |

---

## 10. Summary Test TRX Files
- **Infrastructure Recommendations Suite (29/29 passed):** [gate-b-infra-recommendations.trx](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/tests/gate-b-infra-recommendations.trx)
- **API Recommendations Suite (25/25 passed):** [gate-b-api-recommendations.trx](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/tests/gate-b-api-recommendations.trx)
- **Frontend Recommendation Suite (14/14 passed):** Vitest across `AdminRecommendationsPage.test.tsx` and `RecommendationShelf.test.tsx`
- **E2E Playwright Automation (All passed):**
  - Session Merge Verification: [merge-verification.json](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/merge-verification.json)
  - PDP Smoke Verification: [pdp-smoke.json](file:///c:/Users/manh/Documents/Project3/online-supermarket-system/artifacts/release/2026-09-08/ai/pdp-smoke.json)

