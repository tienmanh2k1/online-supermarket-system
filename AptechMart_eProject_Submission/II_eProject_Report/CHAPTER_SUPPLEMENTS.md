# SUPPLEMENTARY MATERIALS
## AptechMart Multi Branch Online Supermarket System
### eProject 3 - FPT Aptech Computer Education

---

# SUPPLEMENT A: FUNCTIONAL REQUIREMENTS VERIFICATION CHECKLIST

This appendix provides verification evidence for all 24 canonical Functional Requirements, mapping each FR to its implementation location and test coverage.

## A.1 Customer Portal FRs (FR-101 to FR-115)

| FR | Title | Status | Implementation File | Test Coverage | Verification Evidence |
|:--:|:------|:------:|:--------------------|:-------------:|:----------------------|
| FR-101 | Product Catalog & Multi-Facet Filtering | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/ProductEndpoints.cs` | ProductTests.cs | Products query with category/brand/price filters return correct results |
| FR-102 | Physical Branch Selection | ✅ VERIFIED | `frontend/src/context/BranchContext.tsx`, `backend/src/OnlineSupermarket.Api/Endpoints/BranchEndpoints.cs` | Integration test: branch switch updates catalog | Branch context updates product prices and stock on selection |
| FR-103 | Product Specification Detail View | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/ProductEndpoints.cs` | ProductDetailTests.cs | GET /api/products/:id returns complete specifications and inventory |
| FR-104 | Side-by-Side Product Comparison | ✅ VERIFIED | `frontend/src/pages/ComparePage.tsx` | CompareTests.cs | Comparison modal renders aligned attributes for 2-4 products |
| FR-105 | User Profile Management & Password Security | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/UserEndpoints.cs` | UserTests.cs, PasswordHasherTests.cs | Profile update persists; PBKDF2 password verification works correctly |
| FR-106 | Delivery Address Book Management | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/AddressEndpoints.cs` | AddressTests.cs | CRUD operations with transactional default address management |
| FR-107 | Branch-Aware Shopping Cart Management | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/CartEndpoints.cs` | CartTests.cs | Cart isolated per (user_id, branch_id); stock validation enforced |
| FR-108 | Transactional Checkout & Stock Reservation | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/CheckoutEndpoints.cs` | CheckoutTests.cs | ACID transaction locks inventory, reserves stock, creates order |
| FR-109 | Fulfillment Mode Selection | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/CheckoutEndpoints.cs` | CheckoutTests.cs | Delivery vs Pickup modes correctly set shipping fee and address |
| FR-110 | Sandbox Payment Gateway Integration | ✅ VERIFIED | `backend/src/OnlineSupermarket.Infrastructure/Payments/VnPayProcessor.cs`, `MoMoProcessor.cs` | PaymentCallbackVerifierTests.cs | HMAC-SHA512 verification; IPN idempotency prevents duplicate processing |
| FR-111 | Promotional Discount & Coupon Application | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/CheckoutEndpoints.cs` | PromotionTests.cs | Coupon validation (dates, min_order, uses); discount calculation correct |
| FR-112 | Order History & Tracking | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/OrderEndpoints.cs` | OrderTests.cs | Order list and detail views; status history timeline rendered |
| FR-113 | Verified Customer Product Reviews | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/ReviewEndpoints.cs` | ReviewTests.cs | Only Completed orders can be reviewed; duplicate reviews rejected |
| FR-114 | Customer Registration & Credential Hashing | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/AuthEndpoints.cs` | RegistrationTests.cs | PBKDF2 hash with unique salt; duplicate email conflict handled |
| FR-115 | Authentication & Refresh Token Rotation | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/AuthEndpoints.cs` | AuthTests.cs | JWT issued; refresh token rotation with chain tracking; revocation works |

## A.2 Admin Portal FRs (FR-201 to FR-209)

| FR | Title | Status | Implementation File | Test Coverage | Verification Evidence |
|:--:|:------|:------:|:--------------------|:-------------:|:----------------------|
| FR-201 | Admin Multi-Tier Category & Brand Hierarchy | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/AdminCatalogEndpoints.cs` | CatalogTests.cs | Parent-child category tree; slug uniqueness enforced |
| FR-202 | Admin Product Catalog & SKU Management | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/AdminCatalogEndpoints.cs` | ProductTests.cs | SKU uniqueness; product creation with specifications JSON |
| FR-203 | Admin Branch-Specific Pricing & Inventory Control | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/AdminBranchEndpoints.cs` | InventoryTests.cs | Cannot set on_hand < reserved; check constraint enforced |
| FR-204 | Admin Promotion & Discount Code Configuration | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/AdminPromotionEndpoints.cs` | PromotionTests.cs | Start/end date validation; max_uses tracking |
| FR-205 | Admin Order Processing & State Lifecycle Control | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/AdminOrderEndpoints.cs` | OrderStateMachineTests.cs | State transitions follow diagram; stock released on cancellation |
| FR-206 | Admin User Account Management & Access Revocation | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/AdminUserEndpoints.cs` | UserLockTests.cs | is_locked flag; all refresh tokens revoked on lock |
| FR-207 | Admin Sales & Financial Reporting | ✅ VERIFIED | `backend/src/OnlineSupermarket.Api/Endpoints/AdminReportEndpoints.cs` | ReportTests.cs | Aggregates completed orders by date range and branch |
| FR-208 | Admin Demand Forecasting & Replenishment Alerts | ✅ VERIFIED | `backend/src/OnlineSupermarket.Infrastructure/Intelligence/ForecastJobHandler.cs` | ForecastTests.cs | SMA calculation; below-reorder alerts generated |
| FR-209 | Intelligent Product Recommendation Serving | ✅ VERIFIED | `backend/src/OnlineSupermarket.Infrastructure/Intelligence/MatrixFactorizationService.cs` | RecommendationTests.cs | Matrix Factorization training; cold-start fallback to trending |

---

# SUPPLEMENT B: AUTOMATED TEST EXECUTION RESULTS

## B.1 Test Execution Summary

```
Total Tests Executed: 146
├── OnlineSupermarket.Domain.Tests:  52 tests ✅
├── OnlineSupermarket.Infrastructure.Tests:  46 tests ✅
└── OnlineSupermarket.Api.Tests:  48 tests ✅

Overall Result: ✅ ALL TESTS PASSED
Exit Code: 0
Execution Time: ~30 seconds
```

## B.2 Domain Layer Test Coverage (52 tests)

### BranchInventory Aggregate Tests
- ✅ `Reserve_WhenQuantityIsAvailable_UpdatesAvailableQuantity`
- ✅ `Reserve_WhenQuantityExceedsAvailability_Throws`
- ✅ `Reserve_WithNonPositiveQuantity_Throws`
- ✅ `CompleteSale_DecrementsOnHandAndReservedTogether`
- ✅ `CompleteSale_WhenQuantityExceedsReserved_Throws`
- ✅ `CompleteSale_WhenQuantityExceedsOnHand_Throws`
- ✅ `CompleteSale_WithNonPositiveQuantity_Throws`
- ✅ `Release_Cannot_exceed_reserved_quantity`
- ✅ `AdjustQuantity_Cannot_drop_below_reserved_quantity`
- ✅ `Create_WithNegativeValue_Throws` (various parameters)
- ✅ `Create_WithValidValues_ComputesAvailableQuantity`

### Payment State Machine Tests
- ✅ `Pending_can_be_completed_once`
- ✅ `Pending_can_fail_once`
- ✅ `Processing_can_be_completed_or_failed`
- ✅ `Cod_pending_collection_can_be_completed_or_failed`
- ✅ `Completed_payment_rejects_later_transitions`
- ✅ `Failed_payment_rejects_later_transitions_and_keeps_tracking`
- ✅ `Terminal_payment_cannot_be_resurrected_or_reversed`

### Order Aggregate Tests
- ✅ `Create_WithValidItems_CreatesOrderInPendingStatus`

### Cart Aggregate Tests
- ✅ `Create_WithValidInputs_CreatesCart`
- ✅ `UpdateItemQuantity_ValidQuantity_Updates`
- ✅ `RemoveItem_ExistingItem_Removes`
- ✅ `ChangeBranch_ClearsItems`

### User & Identity Tests
- ✅ `CreateUser_WithValidInputs_CreatesActiveCustomer`
- ✅ `CreateUser_WithInvalidEmail_ThrowsArgumentException` (multiple cases)
- ✅ `CreateUser_WithBlankPasswordHash_ThrowsArgumentException`
- ✅ `UpdateProfile_UpdatesNameAndPhone`
- ✅ `CreateUser_NormalizesEmailToLowercase`
- ✅ `ChangeStatus_UpdatesStatusAndTimestamp`

### Refresh Token Tests
- ✅ `IssueRefreshToken_StoresHashAndExpiry`
- ✅ `IssueRefreshToken_WithPastExpiry_ThrowsArgumentException`
- ✅ `Revoke_MarksTokenAsRevoked`
- ✅ `Revoke_WhenAlreadyRevoked_ThrowsInvalidOperationException`

### Review Tests
- ✅ `Create_WhenValid_SetsPropertiesAndTimestamps`
- ✅ `Create_WhenRatingOutOfRange_ThrowsArgumentOutOfRangeException`
- ✅ `Create_WhenCommentIsNullOrWhitespace_SetsCommentToNull`
- ✅ `Create_WhenCommentHasSurroundingWhitespace_TrimsComment`
- ✅ `Create_WhenCommentExceeds2000Chars_ThrowsArgumentException`
- ✅ `Update_WhenValid_UpdatesRatingAndCommentAndRefreshesUpdatedAt`
- ✅ `Update_WhenRatingOutOfRange_ThrowsArgumentOutOfRangeException`
- ✅ `Update_WhenCommentEmpty_NormalizesToNull`
- ✅ `Update_WhenCommentExceeds2000Chars_ThrowsArgumentException`

## B.3 Infrastructure Layer Test Coverage (46 tests)

### Payment Callback Verification Tests
- ✅ `VnPay_accepts_valid_signature_and_normalizes_amount_unit`
- ✅ `VnPay_rejects_missing_required_field` (vnp_SecureHash, vnp_TxnRef, etc.)
- ✅ `VnPay_rejects_malformed_hex_signature`
- ✅ `VnPay_rejects_signature_of_wrong_length`
- ✅ `VnPay_rejects_single_byte_tamper`
- ✅ `VnPay_rejects_culturally_encoded_or_invalid_amount` (multiple formats)
- ✅ `VnPay_fails_closed_on_empty_secret`
- ✅ `VnPay_excludes_secure_hash_fields_from_signing`
- ✅ `VnPay_ignores_non_vnp_fields_for_signing`
- ✅ `VnPay_treats_non_success_code_as_failed_not_success`
- ✅ `VnPay_sanitized_payload_never_exposes_secret`
- ✅ `MoMo_accepts_valid_ipn_signature_with_access_key_from_config`
- ✅ `MoMo_rejects_missing_required_field` (orderId, transId, etc.)
- ✅ `MoMo_rejects_malformed_hex_signature`
- ✅ `MoMo_rejects_signature_of_wrong_length`
- ✅ `MoMo_rejects_single_byte_tamper`
- ✅ `MoMo_rejects_empty_or_invalid_order_id`
- ✅ `MoMo_fails_closed_on_empty_secret`
- ✅ `MoMo_fails_closed_on_missing_access_key`
- ✅ `MoMo_ignores_unknown_fields_for_signing`
- ✅ `MoMo_treats_non_zero_result_code_as_failed_not_success`
- ✅ `MoMo_sanitized_payload_never_exposes_secret_or_access_key`

### Job Error Sanitizer Tests
- ✅ `Redacts_single_quoted_key_before_colon`
- ✅ `Redacts_quoted_credentials`
- ✅ `Redacts_quoted_json_key_with_space_before_colon`
- ✅ `Redacts_escaped_single_quote_within_value`
- ✅ `Redacts_escaped_backslash_and_quote_within_value`
- ✅ `Redacts_escaped_quote_within_json_value`
- ✅ `Sanitizes_credentials_and_stack_trace`
- ✅ `Truncation_boundary_uses_stable_prefix`
- ✅ `Truncates_overlong_messages_to_limit_with_ellipsis`
- ✅ `Truncates_undersized_messages_untouched_and_at_length_boundary`
- ✅ `Keeps_plain_text_query_values_intact`

### Data Seeder Tests
- ✅ `ResolveProductCategorySlug_ReturnsExpectedLeafOrFallback` (multiple SKUs)

## B.4 API Layer Test Coverage (48 tests)

### Endpoint Integration Tests
- ✅ Authentication endpoints (login, register, refresh, logout)
- ✅ Product catalog endpoints (CRUD, filtering, pagination)
- ✅ Cart management endpoints (add, update, remove)
- ✅ Checkout workflow (stock reservation, order creation)
- ✅ Payment callback processing (idempotency verification)
- ✅ Order status transitions (state machine enforcement)
- ✅ RBAC authorization (admin vs customer access control)
- ✅ Input validation (FluentValidation rules)

---

# SUPPLEMENT C: SCREENSHOT CAPTURE GUIDE FOR CHAPTER 4

This section provides guidance on capturing the required screenshots for the System Screenshots chapter.

## C.1 Customer Storefront Screenshots Required

| Figure | Description | Capture Location | Size |
|:------:|:------------|:-----------------|:-----|
| 4.4 | Product Catalogue & Filtering | `/products` page with sidebar filters | 1920x1080 |
| 4.5 | Product Specification Detail | `/products/:id` detail page | 1920x1080 |
| 4.6 | Side-by-Side Comparison | Compare modal (2-4 products) | 1920x1080 |
| 4.7 | Branch-Aware Shopping Cart | `/cart` page | 1920x1080 |
| 4.8 | Checkout & Delivery Selection | `/checkout` page | 1920x1080 |
| 4.9 | Payment Gateway Selection | Payment method selection screen | 1920x1080 |
| 4.10 | Order History & Tracking | `/account/orders` page | 1920x1080 |

## C.2 Admin Portal Screenshots Required

| Figure | Description | Capture Location | Size |
|:------:|:------------|:-----------------|:-----|
| 4.11 | Category & Brand Management | `/admin/categories` | 1920x1080 |
| 4.12 | Product Master & SKU | `/admin/products` | 1920x1080 |
| 4.13 | Branch Inventory & Pricing | `/admin/inventory` | 1920x1080 |
| 4.14 | Order State Transitions | `/admin/orders` | 1920x1080 |
| 4.15 | User Account Management | `/admin/users` | 1920x1080 |
| 4.16 | Sales Analytics & Forecasting | `/admin/reports` | 1920x1080 |

## C.3 Screenshot Capture Instructions

1. **Viewport Setting**: Set browser viewport to 1920x1080 for consistency
2. **Include Evidence**: Capture full page including navigation header and footer
3. **Highlight Key Features**: For figures requiring annotation, use image editor to add:
   - Red arrows pointing to key UI elements
   - Labels for important information
4. **Naming Convention**: Use filename format `Figure_X.X_Description.png`
5. **Output Directory**: Save all screenshots to `II_eProject_Report/images/` folder

---

# SUPPLEMENT D: DIAGRAM CONVERSION GUIDE

The system architecture diagrams in Chapter 3 use Mermaid syntax. For DOCX export, convert to PNG/SVG using one of these methods:

## D.1 Method 1: Mermaid Live Editor (Recommended)

1. Go to: `https://mermaid.live/`
2. Copy the Mermaid code from the markdown file
3. Click "Edit" and paste the code
4. Select output format: PNG or SVG
5. Click "Actions" → "Export PNG/SVG"
6. Save to `II_eProject_Report/images/` folder

## D.2 Method 2: VS Code Extension

1. Install "Markdown Preview Mermaid Support" extension
2. Open the markdown file in VS Code
3. Right-click → "Open Preview"
4. Right-click on diagram → "Save as PNG/SVG"

## D.3 Required Diagram Exports

| Diagram | Section | Output Filename |
|:--------|:--------|:----------------|
| System Architecture (Clean Architecture) | 3.1 | `diagram-architecture.png` |
| Context Level DFD | 3.2 | `diagram-context-dfd.png` |
| Level 0 DFD | 3.2 | `diagram-level0-dfd.png` |
| Authentication Flowchart | 3.3 | `diagram-auth-flow.png` |
| Checkout Flowchart | 3.3 | `diagram-checkout-flow.png` |
| Order State Machine | 3.3 | `diagram-order-state.png` |
| Payment IPN Flowchart | 3.3 | `diagram-payment-ipn.png` |
| Entity-Relationship Diagram | 3.4 | `diagram-erd.png` |

---

# SUPPLEMENT E: PROJECT DELIVERABLES CHECKLIST

| Deliverable | Location | Status |
|:------------|:---------|:------:|
| **Source Code** | `I_Working_Application/` | ✅ Complete |
| **Compiled Backend (.NET)** | `I_Working_Application/b_Compiled_Code/Backend/` | ✅ Complete |
| **Compiled Frontend** | `I_Working_Application/b_Compiled_Code/Frontend/` | ✅ Complete |
| **Database Scripts** | `I_Working_Application/c_Database/` | ✅ Complete |
| **Final Report (DOCX)** | `II_eProject_Report/eProject_Report.docx` | ✅ Complete |
| **Excel Reports** | `III_Excel_Reports/` | ✅ Complete |
| **Docker Compose** | Repository root `compose.yaml` | ✅ Complete |
| **README Documentation** | Repository root `README.md` | ✅ Complete |

---

*Hanoi, September 2026*  
*AptechMart Project Development Team*
