# Thiết kế Reviews, Inventory và AI Intelligence (bản đơn giản hóa)

**Ngày:** 2026-09-03

**Cập nhật:** 2026-09-06

**Trạng thái:** Đã duyệt
**Phạm vi:** FR-113, FR-208, FR-209

## 1. Mục tiêu

Triển khai end-to-end 5 bảng mới: `reviews`, `inventory_transactions`, `product_view_events`, `recommendation_results`, `demand_forecasts`.

Hai bảng được giữ trong thiết kế nhưng **Deferred, không tạo task trong đợt này**:

- `stock_alerts`: chỉ làm sau khi forecast ổn định.
- `background_job_runs`: chỉ cần nếu sau này chuyển sang lịch chạy nền, nhiều instance hoặc cần audit từng lần chạy.

Phạm vi hiện tại gồm domain, migration, API, ML.NET, giao diện và tests. Không có `BackgroundService`, `Channel`, lease, distributed lock hay lịch chạy tự động.

## 2. Số bảng

Baseline hiện tại có 17 bảng vật lý. Thêm 5 bảng active đưa schema mục tiêu lên **22 bảng**. `promotions` đã tồn tại và không thuộc migration mới. Không tạo `UserProductAffinities`; training data được tổng hợp trực tiếp từ views và completed orders.

## 3. Kiến trúc đơn giản

```text
Admin bấm Refresh -> API load dữ liệu -> ML.NET train + predict
                   -> ghi materialized batch -> 200 OK
Customer/Admin UI -> đọc batch mới nhất
```

- Reviews và inventory ledger là nghiệp vụ đồng bộ.
- Recommendation dùng ML.NET `MatrixFactorizationTrainer` trên implicit feedback.
- Forecast dùng ML.NET `ForecastBySsa` trên daily sales series.
- `recommendation_results` và `demand_forecasts` giữ kết quả để UI đọc nhanh.
- Mỗi refresh dùng `batch_id` mới; transaction chỉ công bố batch khi toàn bộ rows đã ghi thành công.

## 4. Data model

### 4.1 `reviews`

`id`, `user_id`, unique `order_item_id`, derived `product_id`, `rating` 1–5, nullable `comment` tối đa 2.000 ký tự, `created_at_utc`, `updated_at_utc`. FK dùng Restrict.

Tạo review chỉ khi OrderItem thuộc order của user hiện tại và order là `Completed`. Một OrderItem chỉ có một review; mua lại ở order khác có thể review lại.

### 4.2 `inventory_transactions`

`id`, `branch_inventory_id`, `transaction_type`, hai delta, hai snapshot sau mutation, `reference_type`, `reference_id`, nullable unique `operation_key`, `actor_user_id`, `note`, `created_at_utc`.

Ledger chỉ INSERT. Mutation và ledger row commit cùng transaction. Batch nhiều sản phẩm lock `BranchInventory` theo `Id` tăng dần để giảm deadlock. `operation_key` chống áp delta hai lần.

### 4.3 `product_view_events`

`id`, `product_id`, nullable `user_id`, nullable `anonymous_session_id`, `viewed_at_utc`. Không lưu IP/user-agent. Khi đăng nhập, merge chỉ gán user cho event anonymous chưa có owner và giữ toàn bộ events.

### 4.4 `recommendation_results`

`id`, `batch_id`, `scope` (Global/User/SimilarProduct), `audience_key`, `product_id`, `score`, `rank`, `algorithm_version`, `generated_at_utc`, `expires_at_utc`. Unique `(batch_id, audience_key, product_id)`.

Personalized results dùng Matrix Factorization. User/product chưa đủ dữ liệu fallback sang top-selling toàn hệ thống.

### 4.5 `demand_forecasts`

`id`, `batch_id`, `branch_inventory_id`, `horizon_days` (7 hoặc 14), `predicted_quantity`, `actual_data_days`, `model_version`, `generated_at_utc`. Unique `(batch_id, branch_inventory_id, horizon_days)`.

Input là completed-order/Sale data group theo ngày và điền 0 cho ngày không bán. Dữ liệu quá ngắn trả `InsufficientData`, không bịa kết quả.

## 5. API contracts

### Reviews

- `GET /api/products/{productId}/reviews` và `/review-eligibility`
- `POST /api/reviews`; `PUT /api/reviews/{reviewId}`
- Order detail bổ sung `canReview` và `reviewId` cho từng item.

### Inventory

- Reserve/release/completed sale/admin adjustment gọi chung `InventoryMutationService`.
- `GET /api/admin/inventory/{inventoryId}/transactions`.

### Recommendations

- `POST /api/products/{productId}/view-events`
- `POST /api/recommendations/session/merge`
- `POST /api/admin/ai/recommendations/refresh` → `200 OK` với `generatedAtUtc`, `resultCount`, `metric`.
- `GET /api/recommendations?branchId={id}`
- `GET /api/products/{productId}/recommendations?branchId={id}`

### Forecast

- `POST /api/admin/ai/forecast/refresh?branchId={id}` → `200 OK` với `generatedAtUtc`, `resultCount`, `metric`.
- `GET /api/admin/forecasts?branchId={id}&horizonDays=7|14`.

Không trả `jobRunId`, không có status polling và không có 202/409 job-lock contract.

## 6. AI được dùng ở đâu

### Recommendation ML

Tổng hợp `(userId, productId, label)`: view trọng số 1, completed purchase trọng số 5. `MatrixFactorizationTrainer` học latent factors và dự đoán score cho sản phẩm user chưa tương tác. Khi đủ dữ liệu, lưu RMSE trên holdout để Admin tham khảo.

### Forecast ML

`ForecastBySsa` học level, trend và pattern từ daily sales của từng branch/product. Model tạo 7 hoặc 14 điểm tương lai rồi cộng thành `predicted_quantity`. Khi đủ dữ liệu, lưu MAE trên holdout.

Global top-selling và `InsufficientData` chỉ là fallback, không được mô tả như model AI.

## 7. UI

- Product detail: reviews, review form và “Có thể bạn cũng thích”.
- Order detail: CTA viết/sửa review sau khi completed.
- Homepage: “Sản phẩm gợi ý cho bạn”.
- Admin Inventory: lịch sử inventory transactions.
- Admin AI Dashboard: last refresh, metric, sample results, hai nút refresh và bảng forecast 7/14 ngày.
- Không làm stock alert UI.

## 8. Phân công và dependency

| Thành viên | Deliverable |
|---|---|
| Thành viên 2 | Product views, recommendation ML/API/result |
| Thành viên 3 | Inventory ledger, daily series, forecast ML/result |
| Thành viên 4 | Completed-order contract, refresh integration, AI Dashboard |

Reviews độc lập. Inventory và product views làm song song. Recommendation cần views/order contract; forecast cần inventory/order contract; Dashboard có thể bắt đầu bằng mock ngay khi DTO chốt.

## 9. Tests tối thiểu

- Domain: review validation, inventory deltas, horizon 7/14.
- Persistence: 5 bảng, FK/unique/check và atomic ledger mutation.
- ML: tiny deterministic dataset; train/predict được; output finite/non-negative; fallback khi thiếu data.
- API: auth, verified purchase, duplicate 409, merge, strict horizon 400, refresh 200.
- Frontend: Vitest + RTL; E2E dùng Playwright trên dev stack.
- Ba smoke flows: completed order → review; anonymous view → refresh → recommendation; completed sales → refresh → forecast.

Không đặt gate 100 branches/5 phút và không bắt buộc multi-instance race test.

## 10. Deferred

- `stock_alerts`: sau này đọc forecast 14 ngày và tồn khả dụng; hiện không có migration/API/UI/test.
- `background_job_runs`: chỉ mở lại khi cần schedule, async polling, audit run hoặc multi-instance recovery.

## 11. Definition of Done

- Schema active đúng 22 bảng.
- Hai pipeline ML.NET chạy bằng refresh và materialize atomically.
- UI đọc result mới nhất, hiển thị rõ empty/insufficient-data.
- Không có active task cho hai bảng deferred.
- Backend tests, frontend tests/build và ba E2E smoke flow pass.
