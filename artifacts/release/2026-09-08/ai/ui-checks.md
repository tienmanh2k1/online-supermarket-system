# UI Verification Report - Gate B Recommendation System

**Date:** 2026-09-08  
**Scope:** Recommendation Shelf, Admin Recommendations Management, Anonymous Merge & Branch Filtering

---

## 1. Automated Test Suite Results

### Frontend Unit & Component Tests (Vitest 4.1.10)
- **Command:** `node node_modules/vitest/vitest.mjs run src/features/recommendations/recommendationSession.test.ts src/features/recommendations/RecommendationShelf.test.tsx src/features/admin/AdminRecommendationsPage.test.tsx src/features/auth/AuthContext.test.tsx`
- **Result:** 4 test files passed, 18/18 tests passed (Duration: 19.12s)

| Test File | Test Cases | Status | Key Verifications |
|---|:---:|:---:|---|
| `recommendationSession.test.ts` | 3 | PASS | Valid GUID creation, corrupted token recovery, rotation on merge |
| `RecommendationShelf.test.tsx` | 7 | PASS | Loading skeleton, empty state suppression, error display & retry re-trigger, branch filtering, PDP similar products |
| `AdminRecommendationsPage.test.tsx` | 5 | PASS | Metadata display, algorithm version, scope selection, paginated results |
| `AuthContext.test.tsx` | 3 | PASS | Automatic `mergeSession` call on login/register with current anonymous ID |

### Backend API Tests (xUnit / .NET 10)
- **Command:** `dotnet test backend/tests/OnlineSupermarket.Api.Tests --no-build --filter "FullyQualifiedName~Recommendation"`
- **Result:** 1 test file matched, 20/20 tests passed (Duration: 4s)
- **Coverage:** Guest view recording with approved schema, branch existence validation, session merge permissions, public recommendations querying with branch filtering and fallback behavior.

---

## 2. End-to-End Anonymous Merge & Security Verification

A live automated browser interaction test was executed via Playwright (`scripts/verify-gate-b-merge.cjs`):

1. **Pre-login Anonymous Event Persistence:**
   - Client browsed product `0baae464-4f4c-49a0-8699-743aa32e28b3` (Dell XPS 15) with session ID `00c202c6-59c0-49c3-80f6-65299fee364d`.
   - Event stored in MySQL `product_view_events` with `user_id = NULL` (Event ID: `29f56f18-28bf-4622-a05a-66c6256a3917`).
2. **Login UI Wiring & Merge Execution:**
   - User submitted credentials via the storefront login form (`LoginForm.tsx`).
   - `AuthContext.login()` automatically dispatched `POST /api/recommendations/session/merge`.
   - Server returned HTTP 200 with `{ "mergedCount": 1 }`.
   - MySQL verified: `product_view_events.user_id` updated to `63cd1842-1e8c-4a34-9dae-170b624c316a` (`mergeuser@test.com`).
3. **Idempotency & Replay Protection:**
   - Repeating `mergeSession` with the same session returned `{ "mergedCount": 0 }`.
4. **Authorization & Cross-Account Isolation:**
   - Calling `mergeSession` without a Bearer token rejected with HTTP 401 Unauthorized.
   - Calling `mergeSession` from another account (`user2@test.com`) returned `{ "mergedCount": 0 }`, and row ownership in MySQL remained strictly with `mergeuser@test.com` (0 ownership leakage).
5. **Signal Window & Recommendation Impact:**
   - The merged product view was immediately recognized in the user's interaction history (seen count: 1).
   - In subsequent recommendation generation, viewed products are excluded from MF candidates, ensuring seen products are not recommended back to the user.

---

## 3. UI State Handling Verification

| State | Component / Page | Behavior Verified | Evidence Source |
|---|---|---|---|
| **Loading** | `RecommendationShelf` | Skeleton loader rendered (`recommendation-shelf-loading`) during asynchronous API fetch. | `RecommendationShelf.test.tsx` (Test: *renders loading skeletons while fetching*) |
| **Empty** | `RecommendationShelf` | When API returns 0 items, shelf container gracefully renders nothing instead of showing broken/empty space. | `RecommendationShelf.test.tsx` (Test: *renders nothing when items are empty after load*) |
| **Error & Retry** | `RecommendationShelf` | Error banner displayed with "Thử lại" button; clicking button invokes `onRetry` callback to refetch without page refresh. | `RecommendationShelf.test.tsx` (Test: *shows error state with retry button that re-triggers*) |
| **Branch Filtering** | `RecommendationShelfLoader` | Request strictly passes active branch ID (`?branchId=...`); out-of-stock items at the selected branch are filtered out before render. | `RecommendationShelf.test.tsx` + `personalized-response.json` (Available stock checked at branch `de4870ff...`) |
| **Admin Metadata** | `AdminRecommendationsPage` | Displays current batch run ID, generated UTC timestamp, and `mf-v1` algorithm version for User scope. | `AdminRecommendationsPage.test.tsx` + `admin-model.png` |

---

## 4. Summary

All criteria for Gate B Bước 4 are satisfied:
- Anonymous event recording -> UI login -> session merge successfully executed and verified in MySQL.
- Security constraints (401 on unauthenticated, 0 on re-merge or cross-account claim) verified.
- UI states (loading, empty, error/retry, branch switching) tested and verified.
