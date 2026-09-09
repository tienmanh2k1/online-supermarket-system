# CHAPTER 3: ePROJECT DESIGN

## 3.1. System Architecture Design

The AptechMart architecture strictly adheres to **Clean Architecture** and **Domain-Driven Design (DDD)** principles. This decoupled design guarantees high testability, maintainability, and domain rule isolation.

```mermaid
graph TD
    subgraph Presentation_Tier ["PRESENTATION TIER"]
        STOREFRONT["React 19 Storefront Client (Vite 7, TypeScript)"]
        ADMIN_PORTAL["Admin Management Dashboard (React 19)"]
        SWAGGER_UI["OpenAPI / Swagger 3.1.1 Documentation"]
    end

    subgraph API_Gateway_Tier ["API & ROUTING TIER"]
        MINIMAL_APIS["ASP.NET Core 10 Minimal APIs Routing Engine"]
        AUTH_MIDDLEWARE["JWT Bearer Authentication & RTR Middleware"]
        VALIDATION_FILTER["FluentValidation Request Filters"]
        LOG_SANITIZER["Sensitive Data Redaction & Exception Handler"]
    end

    subgraph Application_Domain_Tier ["APPLICATION & DOMAIN CORE"]
        DOMAIN_MODELS["Domain Entities & Invariants (BranchInventory, Order, Cart)"]
        STATE_MACHINES["Order & Payment State Machine Processors"]
        BG_WORKERS["Background Job Schedulers & Recovery Executors"]
        ML_RECOMMENDER["Matrix Factorization Recommendation Engine"]
    end

    subgraph Persistence_Infrastructure_Tier ["PERSISTENCE & INFRASTRUCTURE"]
        EF_CORE["Entity Framework Core 10 (ORM & Migrations)"]
        MYSQL_DB[("MySQL 8.4 LTS Database (23 Tables, Relational Schema)")]
    end

    subgraph External_Services ["EXTERNAL THIRD-PARTY SERVICES"]
        VNPAY_GW["VNPay Sandbox Gateway (HMAC-SHA512 IPN)"]
        MOMO_GW["MoMo Sandbox Payment Gateway"]
    end

    STOREFRONT -->|HTTP REST JSON Requests| MINIMAL_APIS
    ADMIN_PORTAL -->|HTTP REST JSON Requests| MINIMAL_APIS
    SWAGGER_UI -->|OpenAPI Contract| MINIMAL_APIS

    MINIMAL_APIS --> AUTH_MIDDLEWARE
    AUTH_MIDDLEWARE --> VALIDATION_FILTER
    VALIDATION_FILTER --> LOG_SANITIZER
    LOG_SANITIZER --> DOMAIN_MODELS

    DOMAIN_MODELS --> STATE_MACHINES
    DOMAIN_MODELS --> EF_CORE
    BG_WORKERS --> DOMAIN_MODELS
    ML_RECOMMENDER --> DOMAIN_MODELS

    EF_CORE -->|Connection Pool & Row-Locks| MYSQL_DB

    MINIMAL_APIS <-->|Redirects & IPN Callbacks| VNPAY_GW
    MINIMAL_APIS <-->|Redirects & IPN Callbacks| MOMO_GW
```

---

## 3.2. Data Flow Diagrams (DFDs)

### Context Level Diagram (System Boundary)
In this Context Diagram, all data flows are strictly labeled by the **transmitted business data payload**:

```mermaid
graph TD
    subgraph External_Entities ["External Entities"]
        GUEST["Guest (Visitor)"]
        CUST["Customer"]
        ADMIN["Supermarket Administrator"]
        PAYMENT["Payment Gateway (VNPay / MoMo / COD)"]
    end

    subgraph System_Core ["AptechMart Multi-Branch System"]
        SYSTEM["AptechMart Core Engine"]
    end

    GUEST -->|Search keywords & branch selection| SYSTEM
    SYSTEM -->|Localized catalog & branch stock status| GUEST

    CUST -->|Registration, credentials, cart items & checkout details| SYSTEM
    SYSTEM -->|Order confirmation, receipts & tracking timeline| CUST

    ADMIN -->|Category/product updates, branch prices & stock allocations| SYSTEM
    SYSTEM -->|Sales metrics, stock replenishment alerts & audit logs| ADMIN

    SYSTEM -->|Signed payment request payload| PAYMENT
    PAYMENT -->|IPN callback & transaction verification result| SYSTEM
```

---

### Level 0 DFD (Subsystem Decomposition)

```mermaid
flowchart TD
    %% External Entities
    GUEST["Guest"]
    CUST["Customer"]
    ADMIN["Administrator"]
    GW["Payment Gateway"]

    %% Data Stores
    D1[("D1: categories, brands, products")]
    D2[("D2: users, refresh_tokens, addresses")]
    D3[("D3: branches, branch_inventories")]
    D4[("D4: carts, cart_items")]
    D5[("D5: orders, order_items, order_status_histories")]
    D6[("D6: payments, payment_callbacks")]
    D7[("D7: reviews")]
    D8[("D8: demand_forecasts, recommendation_results")]

    %% Processes
    P1["P.1 Browse Catalog & Search"]
    P2["P.2 Identity & Address Management"]
    P3["P.3 Shopping Cart Mutation"]
    P4["P.4 Transactional Checkout & Stock Reservation"]
    P5["P.5 Payment Processing & IPN Verification"]
    P6["P.6 Verified Reviews"]
    P7["P.7 Catalog & Product Administration"]
    P8["P.8 Branch Inventory & Pricing Allocation"]
    P9["P.9 Order Fulfillment Lifecycle"]
    P10["P.10 User Governance & Security"]
    P11["P.11 Sales & Analytics Reporting"]
    P12["P.12 Demand Forecast & Recommendation"]

    %% Flows
    GUEST -->|Search query & branch filter| P1
    P1 <-->|Read product details & branch stock| D1
    P1 <-->|Read localized stock| D3
    P1 -->|Product cards & pricing| GUEST

    CUST -->|Credentials & address updates| P2
    P2 <-->|User credentials, tokens & addresses| D2
    P2 -->|Profile state & token pairs| CUST

    CUST -->|Item selections & quantity changes| P3
    P3 <-->|Active cart lines| D4
    P3 <-->|Verify available stock| D3
    P3 -->|Cart subtotal & item counts| CUST

    CUST -->|Checkout confirmation & shipping address| P4
    P4 <-->|Cart contents| D4
    P4 -->|Acquire lock & reserve stock| D3
    P4 -->|Create order snapshot & status history| D5
    P4 -->|Clear cart| D4
    P4 -->|Order ID & payment redirect URL| CUST

    P4 -->|Payment initiation payload| P5
    P5 -->|Redirect payload| GW
    GW -->|Signed IPN callback payload| P5
    P5 <-->|Verify idempotency & record payment| D6
    P5 -->|Update order to Processing| D5

    CUST -->|Product rating & commentary| P6
    P6 <-->|Verify completed purchase| D5
    P6 -->|Save rating| D7

    ADMIN -->|Category, brand & product definitions| P7
    P7 -->|Persist catalog structure| D1

    ADMIN -->|Branch selling prices & replenished quantities| P8
    P8 -->|Update localized stock & prices| D3

    ADMIN -->|Order fulfillment status updates| P9
    P9 <-->|Read pending orders & advance status| D5
    P9 -->|Deduct on-hand & reserved stock upon completion| D3

    ADMIN -->|Lock user account & revoke access| P10
    P10 -->|Revoke refresh tokens| D2

    ADMIN -->|Report filter parameters| P11
    P11 <-->|Aggregate fulfilled order transactions| D5
    P11 -->|Sales performance charts & KPIs| ADMIN

    P12 <-->|Read sales velocity & branch stock| D3
    P12 <-->|Read order history| D5
    P12 -->|Generate restocking alerts & ML scores| D8
    P12 -->|Forecast summary| ADMIN
```

---

## 3.3. Business Process Flowcharts & State Machines

### Authentication & Token Rotation Flowchart

```mermaid
flowchart TD
    START([User Submits Login Credentials]) --> INPUT[Read Email & Password]
    INPUT --> FIND{User Exists in Database?}
    FIND -- No --> REJECT_AUTH[Return 401 Unauthorized]
    FIND -- Yes --> CHECK_LOCKED{Is Account Locked?}
    CHECK_LOCKED -- Yes --> REJECT_LOCKED[Return 403 Forbidden: Account Suspended]
    CHECK_LOCKED -- No --> VERIFY_PW{Verify Salted Hash via PBKDF2}
    VERIFY_PW -- Failed --> REJECT_PW[Return 401 Unauthorized]
    VERIFY_PW -- Matched --> GEN_TOKENS[Generate 15-Min JWT & 7-Day Refresh Token]
    GEN_TOKENS --> SAVE_TOKEN[Persist Refresh Token in DB]
    SAVE_TOKEN --> RETURN_OK([Return HTTP 200 with Token Pair])

    REFRESH_REQ([Access Token Expired: Submit Refresh Token]) --> CHECK_VALID{Token Valid & Unrevoked?}
    CHECK_VALID -- Revoked / Tampered --> DETECT_ATTACK[Security Breach Detected: Revoke All Tokens in Chain]
    DETECT_ATTACK --> REQUIRE_LOGIN([Return 401: Force Full Re-Authentication])
    CHECK_VALID -- Valid --> ROTATE_TOKEN[Revoke Old Token & Issue Replacement Token Pair]
    ROTATE_TOKEN --> SAVE_ROTATION[Persist ReplacedByTokenId Link]
    SAVE_ROTATION --> RETURN_ROTATED([Return HTTP 200 with New Tokens])
```

---

### Transactional Checkout & Stock Reservation Flowchart

```mermaid
flowchart TD
    START([Customer Initiates Checkout]) --> CHECK_AUTH{User Authenticated?}
    CHECK_AUTH -- No --> ERR_AUTH[Return 401 Unauthorized]
    CHECK_AUTH -- Yes --> READ_CART[Load Active Cart for Selected Branch]
    READ_CART --> CART_EMPTY{Cart Has Items?}
    CART_EMPTY -- Yes is Empty --> ERR_EMPTY[Return 400 Bad Request: Cart Empty]
    CART_EMPTY -- Has Items --> OPEN_TX[Open Database Transaction: Repeatable Read]
    
    OPEN_TX --> LOCK_ROWS[Acquire Row-Level Locks on Branch Inventories]
    LOCK_ROWS --> VALIDATE_STOCK{For All Items: Available >= Requested?}
    
    VALIDATE_STOCK -- Insufficient Stock --> ROLLBACK[Rollback Transaction]
    ROLLBACK --> RET_409[Return 409 Conflict with Out-of-Stock Item List]
    
    VALIDATE_STOCK -- Sufficient Stock --> RESERVE[Update reserved_quantity += quantity]
    RESERVE --> CREATE_ORDER[Insert orders Record: Status = Pending]
    CREATE_ORDER --> SNAPSHOT_ITEMS[Insert order_items Snapshots with Price & SKU]
    SNAPSHOT_ITEMS --> CLEAR_CART[Delete cart_items for this Branch]
    CLEAR_CART --> COMMIT_TX[Commit Database Transaction]
    COMMIT_TX --> SUCCESS([Return 201 Created with Order ID])
```

---

### Order Lifecycle State Machine Diagram

```mermaid
stateDiagram-v2
    [*] --> Pending : Customer Places Order (Stock Reserved)
    Pending --> Confirmed : COD Selected
    Pending --> Processing : VNPay / MoMo Callback Success
    Pending --> Cancelled : Customer / System Aborts (Reserved Stock Released)

    Confirmed --> Processing : Store Approves & Begins Fulfillment
    Confirmed --> Cancelled : Order Cancelled by Customer/Admin (Stock Released)

    Processing --> Shipped : Store Dispatches Goods with Delivery Partner
    Processing --> Cancelled : Fulfillment Issue (Stock Released)

    Shipped --> Completed : Customer Receives Order (Stock Deducted Permanently)
    Shipped --> Cancelled : Customer Rejection / Delivery Failed (Stock Released)

    Completed --> [*] : Verified Review Eligible
    Cancelled --> [*] : Transaction Closed
```

---

### Payment Process & IPN Webhook Idempotency Flowchart

```mermaid
flowchart TD
    START([Customer Selects Payment Method]) --> CHECK_METHOD{Method Type}
    
    CHECK_METHOD -- COD --> COD_PATH[Create Payment Record: PendingCollection]
    COD_PATH --> CONFIRM_ORDER[Order Status = Confirmed]
    CONFIRM_ORDER --> RETURN_COD([Display Order Receipt])
    
    CHECK_METHOD -- VNPay / MoMo --> CREATE_PAYMENT[Create Payment: Status = Pending]
    CREATE_PAYMENT --> BUILD_URL[Construct Gateway URL with HMAC-SHA512 Signature]
    BUILD_URL --> REDIRECT([Redirect Customer Browser to Gateway])
    
    GATEWAY_CB([Gateway Sends Webhook IPN Callback]) --> VERIFY_SIG{Verify HMAC-SHA512 Signature}
    VERIFY_SIG -- Invalid Signature --> REJECT_CB[Log Security Warning & Return 400 Bad Request]
    
    VERIFY_SIG -- Valid --> CHECK_IDEMPOTENCY{Payment Already Processed?}
    CHECK_IDEMPOTENCY -- Already Completed --> RETURN_IDEMPOTENT([Return 200 OK: Idempotent No-Op])
    
    CHECK_IDEMPOTENCY -- Pending --> VERIFY_AMT{Callback Amount == Order Total?}
    VERIFY_AMT -- Mismatch --> REJECT_AMT[Flag Fraud Alert & Return 400]
    
    VERIFY_AMT -- Matches --> CHECK_STATUS{Gateway Status Code}
    CHECK_STATUS -- Success --> MARK_COMPLETED[Update Payment = Completed]
    MARK_COMPLETED --> UPDATE_ORDER[Update Order = Processing]
    UPDATE_ORDER --> RECORD_HIST[Insert Status History Entry]
    RECORD_HIST --> ACK_GW([Return HTTP 200 Success to Gateway])
    
    CHECK_STATUS -- Failed / User Cancelled --> MARK_FAILED[Update Payment = Failed]
    MARK_FAILED --> CANCEL_ORDER[Update Order = Cancelled & Release Reserved Stock]
    CANCEL_ORDER --> ACK_GW
```

---

## 3.4. Database Design (ERD & Data Dictionary)

### Entity-Relationship Diagram (ERD - 23 Tables)

```mermaid
erDiagram
    users ||--o{ addresses : "maintains"
    users ||--o{ refresh_tokens : "authenticates_with"
    users ||--o{ password_reset_tokens : "requests_reset"
    refresh_tokens o|--o| refresh_tokens : "replaced_by"

    categories o|--o{ categories : "parent_of"
    categories ||--o{ products : "classifies"
    brands ||--o{ products : "manufactures"
    
    branches ||--o{ branch_inventories : "stocks"
    products ||--o{ branch_inventories : "stocked_at"
    branch_inventories ||--o{ inventory_transactions : "logs"
    users o|--o{ inventory_transactions : "performed_by"

    users ||--o{ carts : "owns"
    branches ||--o{ carts : "bound_to"
    carts ||--|{ cart_items : "contains"
    products ||--o{ cart_items : "references"

    users ||--o{ orders : "places"
    branches ||--o{ orders : "fulfills"
    promotions o|--o{ orders : "discounts"
    orders ||--|{ order_items : "contains"
    products ||--o{ order_items : "snapshotted_from"
    orders ||--|{ order_status_histories : "transitions_through"
    users o|--o{ order_status_histories : "recorded_by"

    orders ||--o{ payments : "settled_via"
    payments ||--o{ payment_callbacks : "records"

    users ||--o{ reviews : "authors"
    products ||--o{ reviews : "evaluated_in"
    order_items ||--o| reviews : "verifies_purchase"

    users o|--o{ product_view_events : "triggers"
    products ||--o{ product_view_events : "targeted_by"
    branches o|--o{ product_view_events : "scoped_to"

    products ||--o{ demand_forecasts : "forecasts"
    branches ||--o{ demand_forecasts : "analyzed_at"

    users ||--o{ recommendation_results : "recommended_to"
    products ||--o{ recommendation_results : "recommended_item"
    branches o|--o{ recommendation_results : "filtered_by"

    branches o|--o{ background_job_runs : "scoped_to"
```

---

### Comprehensive Data Dictionary (All 23 Canonical Tables)

#### 1. `branches` (Physical Supermarket Locations)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique physical branch identifier. |
| `name` | varchar(255) | NO | Non-empty | Physical branch commercial name (e.g. Cau Giay Branch). |
| `code` | varchar(50) | NO | UNIQUE | Unique short code for branch routing. |
| `address` | varchar(500) | NO | Non-empty | Physical street address of the supermarket store. |
| `phone_number`| varchar(20) | YES | Phone format | Direct customer support contact number. |
| `is_active` | tinyint(1) | NO | DEFAULT 1 | Operating status flag. |
| `created_at` | datetime(6) | NO | Timestamp | Branch registration date. |
| `updated_at` | datetime(6) | YES | Timestamp | Last record modification timestamp. |

#### 2. `categories` (Product Classification Hierarchy)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique category identifier. |
| `parent_id` | char(36) | YES | FK -> `categories.id` | Self-referencing parent category pointer. |
| `name` | varchar(255) | NO | Non-empty | Category display name. |
| `slug` | varchar(255) | NO | UNIQUE, Index | SEO-friendly URL slug. |
| `description` | text | YES | | Category marketing description. |
| `display_order`| int | NO | DEFAULT 0 | Visual sort ordering weight. |
| `is_active` | tinyint(1) | NO | DEFAULT 1 | Active visibility toggle. |
| `created_at` | datetime(6) | NO | Timestamp | Category creation timestamp. |
| `updated_at` | datetime(6) | YES | Timestamp | Last update timestamp. |

#### 3. `brands` (Product Manufacturers)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique brand identifier. |
| `name` | varchar(255) | NO | Non-empty | Brand corporate name (e.g. Samsung, LG). |
| `slug` | varchar(255) | NO | UNIQUE, Index | URL slug identifier. |
| `logo_url` | varchar(500) | YES | URL format | Brand logo graphic URL. |
| `is_active` | tinyint(1) | NO | DEFAULT 1 | Active brand status toggle. |
| `created_at` | datetime(6) | NO | Timestamp | Record creation timestamp. |
| `updated_at` | datetime(6) | YES | Timestamp | Modification timestamp. |

#### 4. `products` (Global Product Master Catalog)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique product master identifier. |
| `category_id` | char(36) | NO | FK -> `categories.id` | Associated product category. |
| `brand_id` | char(36) | NO | FK -> `brands.id` | Associated manufacturer brand. |
| `name` | varchar(255) | NO | Index | Global product commercial name. |
| `slug` | varchar(255) | NO | UNIQUE, Index | Unique product URL slug. |
| `sku` | varchar(100) | NO | UNIQUE, Index | Global Stock Keeping Unit (SKU) barcode. |
| `unit` | varchar(50) | NO | Non-empty | Unit of measure (e.g. Piece, Box, Kg). |
| `description` | longtext | YES | | Comprehensive product description. |
| `specifications`| json | YES | Valid JSON | Key-value technical specifications table. |
| `image_urls` | json | YES | Valid JSON | Array of product image URLs. |
| `is_active` | tinyint(1) | NO | DEFAULT 1 | Global product active status. |
| `created_at` | datetime(6) | NO | Timestamp | Record creation timestamp. |
| `updated_at` | datetime(6) | YES | Timestamp | Modification timestamp. |

#### 5. `branch_inventories` (Localized Branch Pricing & Stock)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique branch inventory record ID. |
| `branch_id` | char(36) | NO | FK -> `branches.id`, UQ1 | Supermarket branch location. |
| `product_id` | char(36) | NO | FK -> `products.id`, UQ1 | Stocked product master. |
| `selling_price`| decimal(18,2)| NO | CHECK >= 0 | Localized retail unit price at this branch. |
| `quantity_on_hand`| int | NO | CHECK >= 0, >= reserved | Physical units present in supermarket stockroom. |
| `reserved_quantity`| int | NO | CHECK >= 0 | Units locked for active pending checkouts. |
| `reorder_level`| int | NO | CHECK >= 0, DEFAULT 5 | Minimum threshold triggering replenishment alerts. |
| `created_at` | datetime(6) | NO | Timestamp | Inventory record initial date. |
| `updated_at` | datetime(6) | YES | Timestamp | Last stock or price update timestamp. |

*(Unique index `UQ_branch_inventories_branch_product` enforces one inventory record per branch-product pair).*

#### 6. `users` (Identity & User Accounts)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique user identifier. |
| `email` | varchar(255) | NO | UNIQUE, Index | Unique authentication email address. |
| `password_hash`| varchar(500) | NO | PBKDF2 format | Cryptographically salted PBKDF2 password hash. |
| `full_name` | varchar(255) | NO | Non-empty | User legal name. |
| `phone_number`| varchar(20) | YES | | Contact telephone number. |
| `role` | varchar(50) | NO | DEFAULT 'Customer'| System role (`Customer` or `Admin`). |
| `is_locked` | tinyint(1) | NO | DEFAULT 0 | Administrative lockout toggle. |
| `created_at` | datetime(6) | NO | Timestamp | Account registration timestamp. |
| `updated_at` | datetime(6) | YES | Timestamp | Last profile update. |

#### 7. `addresses` (Customer Delivery Address Book)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique shipping address record ID. |
| `user_id` | char(36) | NO | FK -> `users.id`, Index | Owning customer account. |
| `recipient_name`| varchar(255)| NO | Non-empty | Contact person at delivery destination. |
| `phone_number`| varchar(20) | NO | Non-empty | Delivery contact phone number. |
| `street` | varchar(500) | NO | Non-empty | House number and street name. |
| `ward` | varchar(100) | YES | | Ward or neighborhood jurisdiction. |
| `district` | varchar(100) | NO | Non-empty | Urban district name. |
| `city` | varchar(100) | NO | Non-empty | Province or municipality name. |
| `is_default` | tinyint(1) | NO | DEFAULT 0 | Flag designating primary shipping address. |
| `created_at` | datetime(6) | NO | Timestamp | Address creation date. |
| `updated_at` | datetime(6) | YES | Timestamp | Modification date. |

#### 8. `refresh_tokens` (JWT Refresh Token Rotation)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique token record ID. |
| `user_id` | char(36) | NO | FK -> `users.id`, Index | Token holder account. |
| `token` | varchar(500) | NO | UNIQUE, Index | Cryptographic token string. |
| `expires_at` | datetime(6) | NO | Timestamp | Expiration deadline. |
| `is_revoked` | tinyint(1) | NO | DEFAULT 0 | Revocation status flag. |
| `replaced_by_token_id`| char(36)| YES | FK -> `refresh_tokens.id` | Descendant token reference in rotation chain. |
| `created_at` | datetime(6) | NO | Timestamp | Token issuance timestamp. |

#### 9. `password_reset_tokens` (Account Recovery)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique reset token ID. |
| `user_id` | char(36) | NO | FK -> `users.id`, Index | Target user account. |
| `token` | varchar(500) | NO | UNIQUE | Single-use recovery token. |
| `expires_at` | datetime(6) | NO | Timestamp | Token expiration (usually 1 hour). |
| `is_used` | tinyint(1) | NO | DEFAULT 0 | Single-use redemption flag. |
| `created_at` | datetime(6) | NO | Timestamp | Issuance timestamp. |

#### 10. `carts` (Branch-Scoped Customer Carts)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique cart identifier. |
| `user_id` | char(36) | NO | FK -> `users.id`, Index | Owning customer. |
| `branch_id` | char(36) | NO | FK -> `branches.id` | Physical supermarket branch fulfilling this cart. |
| `created_at` | datetime(6) | NO | Timestamp | Cart creation timestamp. |
| `updated_at` | datetime(6) | YES | Timestamp | Last cart mutation timestamp. |

#### 11. `cart_items` (Individual Cart Line Items)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique cart line ID. |
| `cart_id` | char(36) | NO | FK -> `carts.id`, UQ1 | Owning cart. |
| `product_id` | char(36) | NO | FK -> `products.id`, UQ1 | Selected product. |
| `quantity` | int | NO | CHECK > 0 | Selected unit count. |
| `created_at` | datetime(6) | NO | Timestamp | Addition timestamp. |
| `updated_at` | datetime(6) | YES | Timestamp | Quantity update timestamp. |

#### 12. `promotions` (Discount Campaigns & Coupons)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique promotion record ID. |
| `code` | varchar(50) | NO | UNIQUE, Index | Customer promotional coupon code (e.g. SALE10). |
| `description` | varchar(500) | YES | | Promotional terms and conditions description. |
| `discount_type`| varchar(20) | NO | 'Percentage'/'Fixed' | Discount calculation mechanism. |
| `discount_value`| decimal(18,2)| NO | CHECK > 0 | Discount rate percentage or currency amount. |
| `min_order_amount`| decimal(18,2)| NO | DEFAULT 0 | Minimum cart subtotal qualifying for promotion. |
| `max_discount_amount`| decimal(18,2)| YES | | Ceiling cap for percentage discounts. |
| `max_uses` | int | NO | CHECK > 0 | Total allowed redemptions across all users. |
| `times_used` | int | NO | DEFAULT 0 | Current completed redemptions count. |
| `start_date` | datetime(6) | NO | Timestamp | Campaign activation timestamp. |
| `end_date` | datetime(6) | NO | Timestamp | Campaign expiration timestamp. |
| `is_active` | tinyint(1) | NO | DEFAULT 1 | Manual promotion toggle. |
| `created_at` | datetime(6) | NO | Timestamp | Record creation timestamp. |
| `updated_at` | datetime(6) | YES | Timestamp | Record modification timestamp. |

#### 13. `orders` (Fulfilled and Pending Retail Orders)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique order identifier. |
| `order_number` | varchar(50) | NO | UNIQUE, Index | Human-readable tracking number (e.g. ORD-2026-0001).|
| `user_id` | char(36) | NO | FK -> `users.id`, Index | Customer placing the order. |
| `branch_id` | char(36) | NO | FK -> `branches.id` | Supermarket branch fulfilling and packing goods. |
| `promotion_id` | char(36) | YES | FK -> `promotions.id` | Applied discount campaign (if any). |
| `fulfillment_mode`| varchar(20)| NO | 'Delivery'/'Pickup' | Shipping mode selected by customer. |
| `status` | varchar(30) | NO | Index | State (`Pending`,`Confirmed`,`Processing`,`Shipped`,`Completed`,`Cancelled`). |
| `subtotal` | decimal(18,2)| NO | CHECK >= 0 | Raw item total before discounts and fees. |
| `discount_amount`| decimal(18,2)| NO | DEFAULT 0 | Total discount applied from promotion. |
| `shipping_fee` | decimal(18,2)| NO | DEFAULT 0 | Expedited logistics delivery fee. |
| `total_amount` | decimal(18,2)| NO | CHECK >= 0 | Final grand total payable (`subtotal - discount + fee`).|
| `shipping_address`| json | YES | Valid JSON | Immutable snapshot of recipient shipping coordinates.|
| `notes` | text | YES | | Special delivery instructions from customer. |
| `created_at` | datetime(6) | NO | Timestamp, Index | Order placement timestamp. |
| `updated_at` | datetime(6) | YES | Timestamp | Last state transition timestamp. |

#### 14. `order_items` (Immutable Order Line Snapshots)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique line item identifier. |
| `order_id` | char(36) | NO | FK -> `orders.id`, Index | Associated parent order. |
| `product_id` | char(36) | NO | FK -> `products.id` | Master product reference. |
| `product_name` | varchar(255)| NO | Non-empty | Snapshot of product title at time of purchase. |
| `sku` | varchar(100) | NO | Non-empty | Snapshot of SKU barcode. |
| `unit_price` | decimal(18,2)| NO | CHECK >= 0 | Snapshot of locked unit selling price. |
| `quantity` | int | NO | CHECK > 0 | Number of units purchased. |
| `total_price` | decimal(18,2)| NO | CHECK >= 0 | Line total (`unit_price * quantity`). |
| `created_at` | datetime(6) | NO | Timestamp | Line creation timestamp. |

#### 15. `order_status_histories` (Order Audit Trail)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique status log record ID. |
| `order_id` | char(36) | NO | FK -> `orders.id`, Index | Associated order. |
| `changed_by_user_id`| char(36)| YES | FK -> `users.id` | User or Admin triggering transition. |
| `from_status` | varchar(30) | YES | | Preceding order state. |
| `to_status` | varchar(30) | NO | Non-empty | Consequent order state. |
| `reason` | text | YES | | Context or explanation for status update. |
| `created_at` | datetime(6) | NO | Timestamp | Transition timestamp. |

#### 16. `payments` (Payment Records & Transactions)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique payment transaction ID. |
| `order_id` | char(36) | NO | FK -> `orders.id`, Index | Associated order. |
| `payment_method`| varchar(50) | NO | 'COD'/'VNPay'/'MoMo' | Chosen payment rail. |
| `status` | varchar(30) | NO | Index | State (`Pending`,`Completed`,`Failed`,`Refunded`). |
| `amount` | decimal(18,2)| NO | CHECK >= 0 | Transaction monetary amount. |
| `transaction_id`| varchar(255)| YES | Index | External gateway transaction reference code. |
| `gateway_response`| text | YES | | Serialized payload response from gateway. |
| `created_at` | datetime(6) | NO | Timestamp | Payment initiation timestamp. |
| `updated_at` | datetime(6) | YES | Timestamp | Payment completion or failure timestamp. |

#### 17. `payment_callbacks` (Idempotent Webhook Log)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique callback event ID. |
| `payment_id` | char(36) | NO | FK -> `payments.id` | Associated payment record. |
| `idempotency_key`| varchar(255)| NO | UNIQUE, Index | Unique signature hash preventing duplicate processing.|
| `raw_payload` | longtext | NO | Valid JSON/Form | Raw incoming request payload from gateway IPN. |
| `is_verified` | tinyint(1) | NO | DEFAULT 0 | Checksum signature verification outcome flag. |
| `created_at` | datetime(6) | NO | Timestamp | Webhook arrival timestamp. |

#### 18. `inventory_transactions` (Stock Movement Ledger)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique inventory ledger event ID. |
| `branch_inventory_id`| char(36)| NO | FK -> `branch_inventories.id` | Targeted branch inventory record. |
| `user_id` | char(36) | YES | FK -> `users.id` | Operator performing stock movement. |
| `transaction_type`| varchar(50)| NO | Index | Type (`Reserve`,`Sale`,`CancelRelease`,`Restock`). |
| `quantity_delta`| int | NO | Non-zero | Stock movement magnitude (+/-). |
| `balance_after` | int | NO | CHECK >= 0 | Resulting stock on hand after transaction. |
| `operation_key`| varchar(255)| NO | UNIQUE, Index | Concurrency key preventing duplicate ledger entries. |
| `created_at` | datetime(6) | NO | Timestamp | Ledger recording timestamp. |

#### 19. `reviews` (Verified Customer Product Feedback)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique review identifier. |
| `product_id` | char(36) | NO | FK -> `products.id`, Index | Reviewed product master. |
| `user_id` | char(36) | NO | FK -> `users.id`, Index | Customer submitting review. |
| `order_item_id`| char(36) | NO | FK -> `order_items.id`, UQ1 | Verified completed order line reference. |
| `rating` | int | NO | CHECK (1 <= rating <= 5) | Customer evaluation score (1 to 5 stars). |
| `comment` | text | YES | | Textual review and customer feedback. |
| `is_approved` | tinyint(1) | NO | DEFAULT 1 | Content moderation flag. |
| `created_at` | datetime(6) | NO | Timestamp | Review publication timestamp. |
| `updated_at` | datetime(6) | YES | Timestamp | Last edit timestamp. |

#### 20. `product_view_events` (Clickstream & Analytics Events)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique interaction event ID. |
| `product_id` | char(36) | NO | FK -> `products.id`, Index | Viewed product master. |
| `user_id` | char(36) | YES | FK -> `users.id`, Index | Customer viewing item (null if guest). |
| `branch_id` | char(36) | YES | FK -> `branches.id` | Supermarket branch selected during view. |
| `session_id` | varchar(100) | NO | Index | Browser session correlation key. |
| `viewed_at` | datetime(6) | NO | Timestamp | Exact event timestamp. |

#### 21. `demand_forecasts` (Inventory Demand Forecasting)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique forecast calculation ID. |
| `product_id` | char(36) | NO | FK -> `products.id`, UQ1 | Targeted product. |
| `branch_id` | char(36) | NO | FK -> `branches.id`, UQ1 | Target supermarket branch. |
| `forecast_date`| date | NO | UQ1 | Forecast projection target date. |
| `forecast_quantity`| decimal(18,2)| NO | CHECK >= 0 | Projected units demanded. |
| `confidence_level`| decimal(5,2)| NO | CHECK (0 <= conf <= 1) | Statistical model confidence score. |
| `generated_at` | datetime(6) | NO | Timestamp | Forecast model execution timestamp. |

#### 22. `recommendation_results` (Collaborative Filtering AI)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique recommendation record ID. |
| `user_id` | char(36) | NO | FK -> `users.id`, UQ1 | Target customer receiving recommendation. |
| `product_id` | char(36) | NO | FK -> `products.id`, UQ1 | Recommended product item. |
| `branch_id` | char(36) | YES | FK -> `branches.id` | Filtered supermarket branch scope. |
| `score` | decimal(10,4)| NO | Finite | Predicted Matrix Factorization affinity score. |
| `generated_at` | datetime(6) | NO | Timestamp | Model batch generation timestamp. |

#### 23. `background_job_runs` (Job Scheduling & Concurrency Leases)
| Column Name | Data Type | Nullable | Key / Constraint | Description |
| :--- | :--- | :---: | :--- | :--- |
| `id` | char(36) | NO | PK, UUID v7 | Unique background execution run ID. |
| `job_name` | varchar(100) | NO | Index | Job identifier (e.g. DemandForecastJob, MLTrainJob).|
| `branch_id` | char(36) | YES | FK -> `branches.id` | Physical branch scope (null if global). |
| `status` | varchar(30) | NO | Index | State (`Queued`,`Running`,`Succeeded`,`Failed`). |
| `lease_token` | varchar(100) | YES | Concurrency guard | Distributed lease token preventing dual execution. |
| `lease_expires_at`| datetime(6)| YES | Timestamp | Concurrency lease expiration deadline. |
| `error_summary`| text | YES | Sanitized | Sanitized error diagnostic summary (secrets redacted).|
| `started_at` | datetime(6) | YES | Timestamp | Execution start timestamp. |
| `completed_at` | datetime(6) | YES | Timestamp | Final terminal state timestamp. |
