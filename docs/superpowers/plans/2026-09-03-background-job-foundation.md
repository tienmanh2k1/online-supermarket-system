# Background Job Foundation — Deferred

**Trạng thái:** Deferred, không thuộc scope đồ án hiện tại.

## Quyết định

Phương án cũ dùng `BackgroundService`, `Channel<JobRequest>`, durable run history, lease, recovery và distributed lock đã được bỏ khỏi active plan để giảm độ phức tạp.

Recommendation và forecast hiện chạy theo luồng:

```text
Admin bấm Refresh
  -> API load dữ liệu
  -> ML.NET train + predict
  -> transaction ghi materialized batch
  -> 200 OK
```

Vì vậy không tạo `background_job_runs`, không có task board, scheduler, polling hoặc concurrency lease. `recommendation_results.batch_id` và `demand_forecasts.batch_id` đủ để nhóm một lần sinh kết quả.

## Khi nào mở lại

Chỉ kích hoạt plan này nếu có ít nhất một yêu cầu:

- job phải tự chạy theo lịch;
- request thường xuyên vượt timeout HTTP;
- cần audit lịch sử từng run và lỗi;
- deploy nhiều instance cần claim/lease distributed;
- cần retry/recovery sau khi process chết.

Khi đó phải viết design mới theo codebase hiện tại; không dùng checklist cũ như active plan.

## Tài liệu thay thế

- [Roadmap đơn giản hóa](2026-09-03-reviews-inventory-intelligence-roadmap.md)
- [Recommendation plan](2026-09-03-product-recommendations.md)
- [Inventory và forecast plan](2026-09-03-inventory-forecast-alerts.md)
- [AI Dashboard board](../../tasks/plan-ai-dashboard.html)
