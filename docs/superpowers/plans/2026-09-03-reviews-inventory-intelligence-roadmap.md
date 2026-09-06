# Roadmap Reviews, Inventory và AI Intelligence (đơn giản hóa)

> **For implementers:** Thực hiện theo dependency bên dưới. Mỗi board chỉ còn 3 task đủ để giao cho một người.

**Goal:** Hoàn thành 5 bảng active và hai pipeline ML.NET demo được end-to-end mà không cần background job infrastructure.

**Architecture:** API refresh đồng bộ → train/predict ML.NET → ghi materialized batch → UI đọc batch mới nhất.

**Tech Stack:** .NET 8, EF Core/MySQL, ML.NET (`Microsoft.ML`, `Microsoft.ML.Recommender`, `Microsoft.ML.TimeSeries`), React/Vite, Vitest/RTL, Playwright.

## Scope gate

- Baseline 17 bảng; thêm 5 bảng active; mục tiêu **22 bảng**.
- Active: `reviews`, `inventory_transactions`, `product_view_events`, `recommendation_results`, `demand_forecasts`.
- Deferred, không có task: `stock_alerts`, `background_job_runs`.
- Không có worker, scheduler, channel, lease, lock hoặc polling.

## Dependency map

```text
Contracts
  ├── Reviews (độc lập)
  ├── Inventory Transactions ──> Demand Forecast ML
  └── Product View Events ─────> Recommendation ML
                    Forecast + Recommendation ──> AI Dashboard
                                                   └── Release gate
```

## Sáu task board active

1. [Reviews](../../tasks/plan-reviews.html) — 3 task.
2. [Inventory Transactions](../../tasks/plan-inventory-transactions.html) — 3 task.
3. [Product View Events](../../tasks/plan-product-view-events.html) — 3 task.
4. [Recommendation Results](../../tasks/plan-recommendation-results.html) — 3 task.
5. [Demand Forecasts](../../tasks/plan-demand-forecasts.html) — 3 task.
6. [AI Dashboard](../../tasks/plan-ai-dashboard.html) — 3 task.

Tổng cộng **18 task**. Board Background Job Runs đã được loại khỏi scope active.

## Phân công gợi ý

| Người | Board chính | Handoff |
|---|---|---|
| Thành viên 2 | Product View Events + Recommendation Results | DTO source/result ổn định |
| Thành viên 3 | Inventory Transactions + Demand Forecasts | daily-sales/result DTO ổn định |
| Thành viên 4 | AI Dashboard + completed-order contract | mock theo contract trước, nối API sau |
| Người còn lại | Reviews + release support | review/order-item DTO |

Reviews, Inventory Transactions và Product View Events có thể bắt đầu song song. Hai board ML bắt đầu sau source-data contract tương ứng. Dashboard dựng bằng mock sau khi contract chốt, không cần chờ backend hoàn tất.

## Milestones

### M1 — Data contracts và persistence

Hoàn thành schema/domain/API capture cho Reviews, Inventory Transactions, Product View Events. Gate: migration hướng tới 22 bảng và có seed source data.

### M2 — Hai model ML.NET

Hoàn thành Matrix Factorization và SSA, synchronous refresh, materialized batches và model tests. Gate: sample dataset train/predict thành công; refresh API trả 200.

### M3 — UI và demo

Hoàn thành product/order/home integrations và AI Dashboard. Gate: backend tests, frontend build và ba Playwright smoke flow pass.

## Deferred roadmap

- `stock_alerts`: mở sau khi forecast ổn định.
- `background_job_runs`: mở nếu có yêu cầu scheduled/async/multi-instance execution.
