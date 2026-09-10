# Requirements Traceability — Gate C

Ngày xác minh: 2026-09-09. Evidence chung: `docs/testing/release-test-report.md`, OpenAPI, test suites backend/frontend và artifacts release ngày 07–09/09.

| FR | Trạng thái release | Implementation | Test / evidence |
|---|---|---|---|
| FR-101 | Verified | Product catalog/search/filter | Catalog endpoint/UI tests |
| FR-102 | Verified | Branch selector và branch inventory | Product detail/browse branch tests |
| FR-103 | Verified | Product detail page/API | ProductDetail tests, Gate C browser smoke |
| FR-104 | Deferred | Compare cơ bản tồn tại; registry vẫn DRAFT | Ngoài scope release rút gọn |
| FR-105 | Verified | Profile endpoints/UI | User/profile endpoint tests |
| FR-106 | Verified | Address CRUD | Address endpoint/domain tests |
| FR-107 | Verified | Cart per branch | Cart API/context/page tests |
| FR-108 | Verified | Transactional checkout/reserve | InventoryMutationEndpointTests; C-02 |
| FR-109 | Verified | Pickup/Delivery checkout | Checkout endpoint/UI tests |
| FR-110 | Verified | COD/VNPay/MoMo sandbox callback | Payment callback API/MySQL tests; C-04 |
| FR-111 | Deferred | Coupon flow tồn tại nhưng registry DRAFT | Ngoài scope release; CheckoutCouponTests là regression bổ sung |
| FR-112 | Verified | Order history/detail/status | Order endpoint/UI tests |
| FR-113 | Verified | Verified-purchase reviews | ReviewEndpointsTests; ProductReviews tests; C-01/C-03 |
| FR-114 | Verified | Registration + password hashing | Auth endpoint/domain tests |
| FR-115 | Verified | Login/refresh/logout/reset | Auth endpoint/UI tests |
| FR-201 | Verified | Admin category/brand CRUD | Admin catalog endpoint/UI tests |
| FR-202 | Verified | Admin product CRUD/images | Admin product endpoint/UI tests |
| FR-203 | Verified | Branch inventory/price/ledger | Inventory API/MySQL/UI tests |
| FR-204 | Deferred | Promotion CRUD registry DRAFT | Ngoài scope release rút gọn |
| FR-205 | Verified | Admin orders/status history | Order endpoint/UI tests |
| FR-206 | Verified | Admin user lock/disable | Admin user endpoint/UI tests |
| FR-207 | Verified (minimal) | Sales report by date with completed-order KPI/table | AdminReportingEndpointsTests 17/17; AdminSalesReportPage/App 28/28 |
| FR-208 | Verified | SMA 7/14, `InsufficientData`, branch isolation | Forecast tests; C-09..C-12 |
| FR-209 | Verified | ML.NET MF + content fallback + anonymous merge | Gate B evidence; recommendation tests; C-05..C-08 |

Deferred ở đây là quyết định scope release đã ghi từ ngày 07/09, không được tính là task hoàn thành. 21/24 FR có implementation được xác minh trong phạm vi release; FR-104, FR-111 và FR-204 giữ trạng thái DRAFT/deferred theo registry canonical.
