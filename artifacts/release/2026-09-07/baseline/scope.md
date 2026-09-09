# Scope release rút gọn — 07/09/2026

Nguồn: `docs/superpowers/plans/2026-09-06-solo-five-day-release-plan.md` (chốt ngày 06/09).
Chủ dự án xác nhận phạm vi này; checkpoint EOD ghi scope change nếu đổi.

## Giữ lại (bắt buộc)
- Một model AI recommendation thật (ưu tiên Matrix Factorization bằng ML.NET; CF item-item là phương án dự phòng dùng ngân sách 08/09).
- Forecast: moving average (SMA), `sma-v1`/`average * horizonDays`, hiển thị 7/14; không là SSA/AI đã train.
- Job/result contract hiện tại giữ nguyên (background_job_runs, recommendation_results, demand_forecasts; endpoint job 202).
- Scorer `content-v1` hiện tại giữ làm fallback vận hành (popularity/global). Schema không đổi trong bước POC hôm nay.
- Review, ledger inventory, view events, checkout/payment/stock/auth được kiểm chứng; không xây lại.

## Không làm trong release (theo plan)
- SSA, scheduled retraining, stock alerts, multi-instance/async job mới, performance 100 branches.
- Wishlist, guest checkout, email thật (theo SD registry).
- Không đổi đồng thời kiến trúc jobs/schema.

## Đồng bộ phiên bản & số bảng
- .NET: SDK `10.0.400` (global.json rollForward latestPatch), target `net10.0`, EF Core 10 `MySql.EntityFrameworkCore 10.0.9` (chỉ định trong csproj), không hạ về .NET 8.
- Frontend: React/TypeScript/Vite.
- Số bảng theo `AppDbContextModelSnapshot` (làm từ migration mới): **23 bảng** — gồm `background_job_runs` (23, không phải 22 như spec cũ). `background_job_runs` giữ vì là dependency của recommendation_results/demand_forecasts; công khai khác biệt với spec trong báo cáo.
- Docker cổng 5173/8080; local dev API 5072 tách biệt (kiểm tra A4).

## Quyết định cho FR-209/FR-208
- FR-209 (AI Recommendation): làm trọn pipeline thật, gate B 08/09. Fallback popularity chỉ là vận hành, chưa đủ nghiệm thu AI.
- FR-208 (forecast + stock alert): làm forecast SMA (không alert). Ghi đúng tên SMA.

## Baseline lúc bắt đầu (A1)
- HEAD: `85f48ae` (docs: simplify AI implementation plans).
- Working tree: 51 file modified (7110 insertions, 707 deletions) — phiên làm việc trước về jobs backend (JobRunStore → EfJobRunStore/JobRunExecutor/JobRunPublishGuard, migrations lịch sử, tests) + 14 untracked (artifacts/, docs/progress/, submission/, scripts mới).
- Không stash/reset; làm tuần tự trên working tree này, commit chỉ khi chủ dự án chốt.