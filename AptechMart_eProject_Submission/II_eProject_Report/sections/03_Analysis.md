# CHAPTER 2: ePROJECT ANALYSIS

## 2.1. System Requirements Analysis & Business Workflow

### Multi-Branch Retail Operations Survey
In an enterprise online supermarket network, each physical branch acts as a localized fulfillment node serving a specific geographic radius. The core operational challenge lies in coordinating localized inventory, branch-specific pricing, and centralized customer account management.

```text
[Customer Accesses Site]
          │
          ▼
[Select Nearest Branch] ────────► [Inspect Localized Catalog & Prices]
          │                                      │
          ▼                                      ▼
[Build Branch-Specific Cart] ───► [Transactional Stock Reservation (ACID)]
          │                                      │
          ▼                                      ▼
[Choose Delivery / Pickup] ─────► [Execute Sandbox Payment (COD/VNPay/MoMo)]
          │                                      │
          ▼                                      ▼
[Order Status: Processing] ─────► [Store Fulfills & Delivers Goods]
          │                                      │
          ▼                                      ▼
[Order Status: Completed]  ─────► [Customer Submits Verified Review]
```

---

## 2.2. Functional Requirements Specification (Canonical 24 FRs)

Each functional requirement implemented and verified in the submission package is documented below with formal behavioral specifications:

---

### FR-101: Product Catalog & Multi-Facet Filtering
- **Description**: Allows customers and visitors to browse products, search by keyword, and filter by category, brand, and price range.
- **Actor**: Guest, Customer
- **Preconditions**: Products exist in the catalog and branches are initialized.
- **Main Flow**:
  1. User navigates to `/products`.
  2. System queries active products from database.
  3. User specifies a search keyword or selects category/brand/price filters.
  4. User chooses sorting order (price ascending, descending, or newest).
  5. System returns matching products with paginated metadata.
- **Alternative Flow**:
  - *No matching products found*: System displays a user-friendly "No products found matching your search" message with a reset filter button.
- **Expected Result**: Filtered product list displayed with thumbnail, name, brand, localized price, and stock status.
- **API Endpoint**: `GET /api/products`

---

### FR-102: Physical Supermarket Branch Selection
- **Description**: Enables users to switch between physical supermarket branches, instantly updating localized pricing and stock availability across the entire catalog.
- **Actor**: Guest, Customer
- **Preconditions**: At least one active physical branch exists in the database.
- **Main Flow**:
  1. User clicks the Branch Selector on the navigation header.
  2. System presents dropdown of active branches (e.g. Cau Giay, Dong Da, Ha Dong).
  3. User selects a target branch.
  4. System updates active branch context in client state and storage.
  5. System re-queries catalog endpoints with `branchId` parameter.
- **Alternative Flow**:
  - *Branch has pending cart with items*: System alerts the user that cart contents belong to the previous branch and provides options to clear or keep the previous cart.
- **Expected Result**: Catalog immediately displays localized unit prices and real-time available stock for the newly selected store.
- **API Endpoint**: `GET /api/branches`

---

### FR-103: Product Specification Detail View
- **Description**: Displays full technical specifications, high-resolution photo gallery, detailed descriptions, and localized stock status for a selected product.
- **Actor**: Guest, Customer
- **Preconditions**: Product exists in the catalog.
- **Main Flow**:
  1. User selects a product card.
  2. System loads product details, specifications table, image gallery, and branch inventory.
  3. System renders available stock quantity for the currently active branch.
- **Alternative Flow**:
  - *Product not found or inactive*: System returns `404 Not Found` with redirect to `/products`.
- **Expected Result**: Complete product presentation with quantity selector, stock indicator, and specifications.
- **API Endpoint**: `GET /api/products/:id`

---

### FR-104: Side-by-Side Product Comparison
- **Description**: Allows users to select 2 to 4 products within the same category to compare technical specifications and localized prices side by side.
- **Actor**: Guest, Customer
- **Preconditions**: Products must belong to the same category.
- **Main Flow**:
  1. User clicks the "Compare" icon on a product.
  2. System adds the product to client comparison state.
  3. User selects a second product in the same category.
  4. System renders comparison modal overlay with aligned technical attribute rows.
- **Alternative Flow**:
  - *User attempts to compare items from different categories*: System displays validation warning: "Comparison is only supported for products in the same category."
  - *User attempts to compare > 4 items*: System alerts that the 4-item comparison ceiling has been reached.
- **Expected Result**: Clean side-by-side comparison matrix with direct "Add to Cart" links.
- **API Endpoint**: `GET /api/products`

---

### FR-105: User Profile Management & Password Security
- **Description**: Allows authenticated customers to view and update their profile details (full name, phone number) and securely update their password.
- **Actor**: Customer
- **Preconditions**: Customer is logged in with valid JWT access token.
- **Main Flow**:
  1. Customer navigates to `/account/profile`.
  2. System loads current profile data.
  3. Customer updates full name or phone number and submits form.
  4. System validates inputs and persists changes.
  5. For password changes: Customer supplies current password, new password, and confirmation; system verifies current password via PBKDF2 and updates hash.
- **Alternative Flow**:
  - *Incorrect current password*: System rejects request with `400 Bad Request` ("Current password incorrect").
- **Expected Result**: Profile and security credentials updated successfully.
- **API Endpoint**: `PUT /api/users/me`

---

### FR-106: Delivery Address Book Management
- **Description**: Enables customers to maintain multiple shipping addresses (CRUD) with exactly one address marked as default through transactional guarantees.
- **Actor**: Customer
- **Preconditions**: Customer is logged in.
- **Main Flow**:
  1. Customer navigates to `/account/addresses`.
  2. Customer clicks "Add New Address", fills in recipient name, phone, street address, district, city, and checks "Set as default".
  3. System initiates database transaction: resets previous default flag for this user and sets new address as default.
  4. Customer can edit or delete existing non-default addresses.
- **Alternative Flow**:
  - *Customer deletes current default address while other addresses exist*: System automatically designates the oldest remaining address as default.
- **Expected Result**: Accurate multi-point address book with exactly one default address.
- **API Endpoint**: `POST /api/users/me/addresses`

---

### FR-107: Branch-Aware Shopping Cart Management
- **Description**: Manages customer shopping carts isolated per user and physical branch, validating real-time available stock levels upon every quantity mutation.
- **Actor**: Customer
- **Preconditions**: Customer is logged in; physical branch is selected.
- **Main Flow**:
  1. Customer clicks "Add to Cart" on a product.
  2. System validates that requested quantity <= `(quantity_on_hand - reserved_quantity)`.
  3. System creates or updates `cart_items` associated with `(user_id, branch_id)`.
  4. Customer adjusts quantities or removes items in cart view; cart subtotals update in real time.
- **Alternative Flow**:
  - *Requested quantity exceeds available stock*: System rejects mutation and caps quantity to current available stock with an informative notice.
- **Expected Result**: Persistent, branch-aware cart with strictly validated item quantities.
- **API Endpoint**: `POST /api/cart`

---

### FR-108: Transactional Checkout & Stock Reservation
- **Description**: Executes atomic checkout by re-validating prices, re-checking inventory, reserving stock (`reserved_quantity += quantity`), and generating an immutable order snapshot.
- **Actor**: Customer
- **Preconditions**: Cart contains at least 1 item; all items are available at chosen branch.
- **Main Flow**:
  1. Customer clicks "Proceed to Checkout" from `/cart`.
  2. System opens ACID database transaction (`REPEATABLE READ`).
  3. System acquires row-level locks on `branch_inventories` for all cart items.
  4. System verifies current stock and current prices.
  5. System increases `reserved_quantity` for each item.
  6. System creates `orders` record and itemized `order_items` snapshots (name, SKU, unit price).
  7. System clears the customer's cart for this branch.
  8. Transaction commits.
- **Alternative Flow**:
  - *Concurrent shopper purchased remaining stock*: System aborts transaction, rolls back changes, and returns `409 Conflict` with itemized out-of-stock details.
- **Expected Result**: Order created in `Pending` status; stock reserved; cart cleared; zero overselling.
- **API Endpoint**: `POST /api/checkout`

---

### FR-109: Fulfillment Mode Selection
- **Description**: Allows customers to select between Store Pickup and Home Delivery during checkout, capturing recipient details or collection branch snapshots.
- **Actor**: Customer
- **Preconditions**: Order is being configured during checkout.
- **Main Flow**:
  1. Customer selects fulfillment tab: "Home Delivery" or "Store Pickup".
  2. If Home Delivery: Customer selects an address from the address book.
  3. If Store Pickup: Customer confirms collection at the branch where stock is reserved.
  4. System calculates shipping fees (if applicable) and updates order total.
- **Alternative Flow**:
  - *Delivery address missing required fields*: System flags validation errors before allowing checkout submission.
- **Expected Result**: Order record accurately snapshots delivery method and shipping coordinates.
- **API Endpoint**: `POST /api/checkout`

---

### FR-110: Sandbox Payment Gateway Integration
- **Description**: Integrates Cash on Delivery (COD), VNPay Sandbox, and MoMo Sandbox payment options, supporting secure redirection and idempotent callback processing.
- **Actor**: Customer
- **Preconditions**: Order created with valid reserved stock.
- **Main Flow**:
  1. Customer chooses payment method (COD, VNPay, or MoMo).
  2. For COD: Order transitions to `Confirmed`; payment marked `PendingCollection`.
  3. For VNPay/MoMo: Backend constructs signed query URL with HMAC-SHA512 checksum.
  4. Customer completes sandbox authorization on gateway page.
  5. Gateway issues Webhook IPN to `POST /api/checkout/payment/callback`.
  6. Backend verifies HMAC signature, matches payment amount, marks payment `Completed`, and transitions order to `Processing`.
- **Alternative Flow**:
  - *Gateway duplicate IPN callback received*: Backend detects previously completed transaction via idempotency check and returns HTTP 200 without duplicate execution.
  - *Invalid signature or tampered amount*: Backend rejects callback with HTTP 400 and logs security alert.
- **Expected Result**: Tamper-proof, idempotent payment processing with accurate state transition.
- **API Endpoint**: `POST /api/checkout/payment`, `POST /api/checkout/payment/callback`

---

### FR-111: Promotional Discount & Coupon Application
- **Description**: Allows customers to enter coupon codes during checkout to receive percentage or fixed amount discounts based on business rules.
- **Actor**: Customer
- **Preconditions**: Active promotional promotion exists in database.
- **Main Flow**:
  1. Customer enters promo code in checkout summary.
  2. System verifies validity: current timestamp within `[start_date, end_date]`, order subtotal >= `min_order_amount`, and `times_used < max_uses`.
  3. System calculates discount amount and recalculates final order total.
- **Alternative Flow**:
  - *Coupon expired or minimum order requirement not met*: System displays clear rejection error.
- **Expected Result**: Discount applied to order snapshot and coupon usage counter incremented upon order placement.
- **API Endpoint**: `POST /api/checkout/coupon`

---

### FR-112: Order History & Tracking
- **Description**: Provides customers with an order dashboard showing current processing status, tracking history, item snapshots, and payment details.
- **Actor**: Customer
- **Preconditions**: Customer is logged in and has placed orders.
- **Main Flow**:
  1. Customer navigates to `/account/orders`.
  2. System retrieves orders belonging to customer, sorted newest first.
  3. Customer clicks "View Details" on an order.
  4. System renders order items, unit prices, branch information, status history timeline, and payment record.
- **Alternative Flow**:
  - *Customer attempts to access an order belonging to another user*: Backend returns `403 Forbidden` or `404 Not Found`.
- **Expected Result**: Transparent order tracking with complete historical snapshots.
- **API Endpoint**: `GET /api/orders`, `GET /api/orders/:id`

---

### FR-113: Verified Customer Product Reviews
- **Description**: Enables customers who purchased a product in a fulfilled (`Completed`) order to submit a 1 to 5-star rating with commentary.
- **Actor**: Customer
- **Preconditions**: Customer owns at least one order containing the product with status `Completed`.
- **Main Flow**:
  1. Customer navigates to product page or order history.
  2. System verifies completed purchase in `order_items`.
  3. Customer selects star rating (1–5) and inputs textual review.
  4. System persists review record and updates aggregate rating score for product.
- **Alternative Flow**:
  - *Customer has not purchased item or order is not completed*: System disables review submission and displays "Verified purchase required to leave a review".
  - *Customer attempts duplicate review for same order item*: System rejects with `400 Bad Request`.
- **Expected Result**: Authentic customer review published with "Verified Purchase" badge.
- **API Endpoint**: `POST /api/reviews`

---

### FR-114: Customer Registration & Credential Hashing
- **Description**: Enables new customers to register accounts with unique email validation and PBKDF2/HMAC-SHA256 salted password hashing.
- **Actor**: Customer
- **Preconditions**: User is not logged in.
- **Main Flow**:
  1. User fills registration form with email, full name, phone, and password.
  2. Backend validates password complexity (>= 8 characters, uppercase, lowercase, number).
  3. Backend generates cryptographically secure 16-byte salt and derives 32-byte PBKDF2 hash.
  4. New record created in `users` with role `Customer`.
- **Alternative Flow**:
  - *Email already registered*: System returns `409 Conflict` ("Email is already in use").
- **Expected Result**: Secure user account provisioned in database.
- **API Endpoint**: `POST /api/auth/register`

---

### FR-115: Authentication & Refresh Token Rotation
- **Description**: Manages user authentication, short-lived JWT token issuance, secure Refresh Token Rotation (RTR), logout, and token revocation.
- **Actor**: Customer, Admin
- **Preconditions**: User is registered.
- **Main Flow**:
  1. User submits email and password to `/api/auth/login`.
  2. System validates hash; issues 15-minute JWT Access Token and persistent Refresh Token (7-day validity).
  3. On access token expiration: Client sends refresh token to `/api/auth/refresh`.
  4. System verifies refresh token, revokes it (`is_revoked = true`), links it to a replacement token (`replaced_by_token_id`), and issues a new token pair.
- **Alternative Flow**:
  - *Attempted reuse of an already revoked refresh token*: System detects potential token compromise, revokes all descendant tokens in the chain, and requires user re-authentication.
- **Expected Result**: Seamless, highly secure authentication lifecycle.
- **API Endpoint**: `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`

---

### FR-201: Admin Multi-Tier Category & Brand Hierarchy
- **Description**: Empowers administrators to create, update, and soft-delete multi-level category trees (parent-child relationships) and brand catalogues.
- **Actor**: Admin
- **Preconditions**: Authenticated with `Admin` role.
- **Main Flow**:
  1. Admin navigates to `/admin/categories`.
  2. Admin creates root or sub-categories with unique URL slug and display order.
  3. Admin manages brand portfolios with logos and descriptions.
  4. System validates hierarchy tree preventing cyclical dependencies.
- **Alternative Flow**:
  - *Non-admin accesses endpoint*: System returns `403 Forbidden`.
- **Expected Result**: Standardized global category tree maintained.
- **API Endpoint**: `POST /api/admin/catalog/categories`, `PUT /api/admin/catalog/categories/:id`

---

### FR-202: Admin Product Catalog & SKU Management
- **Description**: Allows administrators to maintain product entities, global SKU codes, units of measure, specifications, and image galleries.
- **Actor**: Admin
- **Preconditions**: Category and brand exist.
- **Main Flow**:
  1. Admin navigates to `/admin/products` and clicks "New Product".
  2. Admin fills product name, global SKU code, category, brand, unit of measure, and specifications.
  3. System validates SKU uniqueness across system and creates product record.
- **Alternative Flow**:
  - *Duplicate SKU*: System returns `409 Conflict` ("SKU code already exists").
- **Expected Result**: Global product definition created, ready for branch inventory stocking.
- **API Endpoint**: `POST /api/admin/catalog/products`, `PUT /api/admin/catalog/products/:id`

---

### FR-203: Admin Branch-Specific Pricing & Inventory Control
- **Description**: Allows administrators to assign selling prices, replenish on-hand stock (`quantity_on_hand`), and set reorder thresholds independently for each branch.
- **Actor**: Admin
- **Preconditions**: Product and physical branch exist.
- **Main Flow**:
  1. Admin selects a branch and product in `/admin/inventory`.
  2. Admin inputs unit selling price (`selling_price`), stock on hand, and reorder level.
  3. System verifies `selling_price >= 0` and `quantity_on_hand >= reserved_quantity`.
  4. System updates `branch_inventories` record and logs audit entry.
- **Alternative Flow**:
  - *Attempt to set on-hand stock lower than currently reserved stock*: System rejects with `400 Bad Request` to prevent inventory invariant corruption.
- **Expected Result**: Branch inventory updated safely without breaking concurrency guarantees.
- **API Endpoint**: `PUT /api/admin/branches/:id/inventory`

---

### FR-204: Admin Promotion & Discount Code Configuration
- **Description**: Allows administrators to configure promotional campaigns, coupon codes, percentage/fixed discounts, validity periods, and usage quotas.
- **Actor**: Admin
- **Preconditions**: Authenticated with `Admin` role.
- **Main Flow**:
  1. Admin navigates to `/admin/promotions` and clicks "Create Promotion".
  2. Admin sets coupon code, discount percentage or amount, start/end dates, minimum order value, and maximum redemption quota.
  3. System validates date ranges and persists promotion.
- **Alternative Flow**:
  - *End date earlier than start date*: Validation error prevents saving.
- **Expected Result**: Active promotional campaign available for checkout application.
- **API Endpoint**: `POST /api/admin/promotions`

---

### FR-205: Admin Order Processing & State Lifecycle Control
- **Description**: Enables store managers to view orders across branches, filter by status, and transition orders through fulfillment stages.
- **Actor**: Admin
- **Preconditions**: Orders exist in system.
- **Main Flow**:
  1. Admin opens `/admin/orders` and filters by branch and status.
  2. Admin inspects order items, delivery method, and payment status.
  3. Admin transitions order from `Processing` to `Shipped`, and finally to `Completed`.
  4. System records transition in `order_status_histories` and deducts both `quantity_on_hand` and `reserved_quantity` upon order completion.
- **Alternative Flow**:
  - *Admin cancels an active order*: System transitions status to `Cancelled` and releases reserved stock (`reserved_quantity -= quantity`).
- **Expected Result**: Full lifecycle compliance with domain state machine and zero inventory drift.
- **API Endpoint**: `GET /api/admin/orders`, `POST /api/admin/orders/:id/status`

---

### FR-206: Admin User Account Management & Access Revocation
- **Description**: Empowers administrators to inspect registered users, manage roles, and lock/unlock accounts violating platform policies.
- **Actor**: Admin
- **Preconditions**: Target user account exists.
- **Main Flow**:
  1. Admin searches users by email or name in `/admin/users`.
  2. Admin toggles "Lock Account" on an offending user.
  3. System sets `is_locked = true` and revokes all active refresh tokens immediately.
  4. Locked user cannot log in and existing access tokens are rejected upon expiration.
- **Alternative Flow**:
  - *Admin attempts to lock their own account*: System prevents self-lockout with validation guard.
- **Expected Result**: Immediate revocation of access for suspended accounts.
- **API Endpoint**: `GET /api/admin/users`, `POST /api/admin/users/:id/lock`

---

### FR-207: Admin Sales & Financial Reporting
- **Description**: Aggregates sales volume, gross revenue, net revenue after discounts, and fulfillment metrics filtered by date range, branch, and category.
- **Actor**: Admin
- **Preconditions**: Completed orders exist.
- **Main Flow**:
  1. Admin opens `/admin/reports/sales`.
  2. Admin selects date range (last 7 days, 30 days, quarter) and optional branch filter.
  3. System aggregates data from `orders` and `order_items` where `status = 'Completed'`.
  4. System displays summary KPIs (Total Revenue, Orders Fulfilled, Average Order Value) and visual trend charts.
- **Alternative Flow**:
  - *No completed orders in date window*: System renders zero-state report cleanly.
- **Expected Result**: Accurate business intelligence reflecting actual completed transactions.
- **API Endpoint**: `GET /api/admin/reports/sales`

---

### FR-208: Admin Demand Forecasting & Replenishment Alerts
- **Description**: Analyzes historical sales velocity and branch stock levels to generate replenishment alert notifications for products falling below reorder levels.
- **Actor**: Admin
- **Preconditions**: Branch inventories populated.
- **Main Flow**:
  1. Background job or Admin triggers demand forecast analysis.
  2. System computes Simple Moving Average (SMA) of 30-day order velocity.
  3. System compares `available_quantity` against `reorder_level`.
  4. System displays warning list of items requiring immediate restocking.
- **Alternative Flow**:
  - *All inventory items above reorder threshold*: System indicates healthy stock status.
- **Expected Result**: Actionable restocking alerts preventing store stockouts.
- **API Endpoint**: `GET /api/admin/forecast`

---

### FR-209: Intelligent Product Recommendation Serving
- **Description**: Generates personalized product recommendations for customers based on collaborative filtering Matrix Factorization trained on user interaction events.
- **Actor**: Admin (Monitoring/Trigger), Customer (Receiver)
- **Preconditions**: User interaction events recorded in `product_view_events` and orders.
- **Main Flow**:
  1. Background ML service trains Matrix Factorization model on user-item interaction matrix.
  2. Model predicts top-N product recommendation scores for users.
  3. Recommendations are cached in `recommendation_results`.
  4. When customer views storefront, personalized recommendations appear on home/detail pages.
- **Alternative Flow**:
  - *New user without interaction history (Cold-Start)*: System automatically falls back to trending/bestselling products in the customer's selected branch.
- **Expected Result**: Non-zero, finite recommendation scores enhancing cross-selling.
- **API Endpoint**: `GET /api/admin/recommendations`

---

## 2.3. Non-Functional Requirements (NFRs)

| Category | Requirement Specification |
| :--- | :--- |
| **Performance** | API response time <= 200ms for 95% of catalog read requests under normal load. Static frontend bundles optimized (< 500KB gzipped) with Vite code-splitting. |
| **Data Integrity & Concurrency** | Zero overselling guarantee enforced by database check constraints: `quantity_on_hand >= 0`, `reserved_quantity >= 0`, and `quantity_on_hand >= reserved_quantity`. Repeatable Read transactions on checkout. |
| **Security** | Passwords salted and hashed with PBKDF2 (10,000+ iterations, HMAC-SHA256). Short-lived JWT tokens (15-min) with Refresh Token Rotation. Payment webhooks validated using HMAC-SHA512 checksums. Sensitive error messages sanitized in background logs. |
| **Reliability & Idempotency** | Webhook callbacks from payment gateways process idempotently using unique transaction keys. Background job recovery mechanisms automatically detect and recover orphaned tasks upon server restart. |
| **Scalability & Portability** | Stateless API design enabling horizontal scaling. Containerized via Docker Compose with dedicated database volumes and isolated service networking. |

---

## 2.4. Role-Based Access Control (RBAC) Matrix

| System Resource / API Area | Guest | Customer | Administrator |
| :--- | :---: | :---: | :---: |
| Browse Catalog, Categories, Brands | READ | READ | READ |
| Select Supermarket Branch | READ | READ | READ |
| View Product Details & Compare | READ | READ | READ |
| Manage Shopping Cart | ✗ | READ / WRITE | READ |
| Checkout & Order Placement | ✗ | READ / WRITE | ✗ |
| View Personal Order History | ✗ | READ | ✗ |
| Submit Verified Product Review | ✗ | WRITE | ✗ |
| Manage Profile & Addresses | ✗ | READ / WRITE | ✗ |
| Admin Catalog CRUD (Category, Brand, Product) | ✗ | ✗ | READ / WRITE |
| Admin Branch Inventory & Pricing Adjustments | ✗ | ✗ | READ / WRITE |
| Admin Order State Lifecycle Operations | ✗ | ✗ | READ / WRITE |
| Admin User Account Control & Token Revocation | ✗ | ✗ | READ / WRITE |
| Admin Sales Reports & Inventory Intelligence | ✗ | ✗ | READ |
