# CHAPTER 1: ePROJECT SYNOPSIS

## 1.1. Project Title & Real-World Context

- **Project Title**: **AptechMart Multi Branch Online Supermarket System**
- **Academic Domain**: Project 3 (eProject) — Higher Diploma in Software Engineering (HDSE)
- **Institution**: FPT Aptech Computer Education

### Real-World Business Context
In the contemporary retail and digital commerce landscape, omnichannel grocery and supermarket chains operate complex networks of physical retail branches distributed across diverse districts and metropolitan regions. Traditional single-warehouse e-commerce platforms suffer from acute structural limitations when applied to supermarket chains:
1. **Inventory Location Mismatch (Overselling & Logistics Lag)**: In a single-inventory pool architecture, an online shopper in District A may order items that are physically out of stock in the branch nearest to them. Fulfilling the order requires cross-docking or shipping from a distant warehouse, resulting in high logistics costs, long delivery delays, and spoilage of perishable goods.
2. **Pricing Disparity Across Branches**: Operational expenditures, commercial rent, and regional supplier agreements vary across physical locations. A supermarket branch situated in a central metropolitan shopping district may have different operating overheads and promotional pricing than a suburban branch. Legacy platforms lack the capability to maintain branch-specific selling prices on a unified product catalog.
3. **Lack of Seamless Omnichannel Customer Choice**: Customers increasingly expect flexible fulfillment options: selecting between **Store Pickup** (reserving goods at a chosen supermarket branch and collecting them in person) and **Home Delivery** (expedited shipping from the closest neighborhood branch). Without branch-aware inventory, customers cannot ascertain whether their local store possesses the desired goods.

### The AptechMart Solution
**AptechMart** is designed from the ground up to overcome these challenges through an enterprise-grade **Multi-Branch Inventory & Pricing Architecture**:
- **Global Catalog Foundation**: A standardized, unified product hierarchy (`categories`, `brands`, `products`, `SKU`) is maintained centrally.
- **Distributed Branch Inventory**: Each physical supermarket branch (`branches`) maintains independent inventory ledgers (`branch_inventories`) with isolated selling prices (`selling_price`), on-hand stock (`quantity_on_hand`), reserved allocations (`reserved_quantity`), available stock (`available_quantity = quantity_on_hand - reserved_quantity`), and reorder thresholds (`reorder_level`).
- **Branch-Aware Cart & Atomic Stock Reservation**: A customer's shopping cart (`carts`) is bound to their selected physical supermarket branch. During checkout, an atomic database transaction verifies stock availability and places an immediate reservation lock (`reserved_quantity`), completely preventing race conditions and overselling.

---

## 1.2. Project Objectives

### Business & Operational Objectives
1. **End-to-End Retail Journey**: Provide a frictionless digital shopping experience spanning localized product browsing, category filtering, branch selection, side-by-side product comparison, branch-aware carts, transactional checkout, multi-gateway payments, and order tracking.
2. **Multi-Gateway Payment Integration**: Provide diverse, secure payment options including Cash on Delivery (COD), VNPay Sandbox, and MoMo Sandbox with tamper-proof HMAC-SHA512 signature verification and idempotent IPN callback processing.
3. **Comprehensive Admin Portal**: Empower supermarket store managers and corporate administrators with tools to manage multi-tier category trees, brand portfolios, product catalogs, branch pricing and stock levels, order status lifecycle workflows, and customer accounts.

### Technical & Architectural Objectives
1. **Clean Architecture & Domain-Driven Design**: Strict layer separation across Domain, Application, Infrastructure, and Presentation tiers to maximize maintainability, testability, and enterprise extensibility.
2. **Strict Concurrency & Data Integrity**: Leverage database transaction scopes, row-level locks, and native check constraints to guarantee that `available_quantity` and `quantity_on_hand` never drop below zero, even under heavy concurrent load.
3. **Modern Identity & Token Security**: Implement stateless JSON Web Token (JWT) Bearer authentication coupled with **Refresh Token Rotation (RTR)** to ensure high security and mitigate replay/theft attacks.
4. **Automated Verification**: Complete automated testing covering Domain Unit Tests, Infrastructure Integration Tests, Minimal API Endpoint Tests, and Frontend Vitest suites.
5. **Containerized Deployment**: Single-command orchestrations via Docker Compose ensuring identical staging, evaluation, and production environments.

---

## 1.3. System User Roles (Actors)

| Actor | Description & System Permissions |
| :--- | :--- |
| **Guest (Unauthenticated Visitor)** | Can browse public product catalogs, search keywords, filter by category/brand/price range, view product technical specifications, switch active supermarket branches to inspect localized stock availability and prices, and compare specifications between 2–4 products. |
| **Customer (Registered Member)** | Possesses all Guest privileges plus authenticated capabilities: manage profile details, maintain a multi-point shipping address book with a transactional default address, create and maintain branch-specific shopping carts, execute checkout, select delivery vs. pickup, pay via sandbox gateways, monitor order history, and submit verified product reviews on fulfilled orders. |
| **Administrator (System Admin / Manager)** | Corporate and operational personnel. Full administrative privileges: create/update/soft-delete multi-tier categories and brands, manage products and SKU identifiers, adjust localized prices and replenish stock per branch, advance order state machines, ban/unban user accounts, trigger batch analytics jobs, and inspect sales and demand forecast reports. |

---

## 1.4. Project Scope & Scope Management

### Core Functional Scope (24 Canonical Functional Requirements)
The system delivers 24 canonical functional requirements divided across two primary portals:
- **Customer Portal**: FR-101 to FR-115 (Browse, Branch Selection, Detail View, Comparison, Profile, Addresses, Cart, Checkout, Delivery Selection, Sandbox Payments, Coupons, Order History, Reviews, Registration, Authentication).
- **Admin Portal**: FR-201 to FR-209 (Category/Brand CRUD, Product/SKU CRUD, Branch Inventory & Price Management, Promotions CRUD, Order Management, User Account Controls, Sales Reporting, Demand Forecast Alerts, Recommendation Intelligence).

### Scope Drift Management
To ensure that development efforts remained strictly focused on core transactional integrity and system reliability, non-essential secondary features were formally classified and controlled:
- `SD-001 (Wishlist / Favorites)`: Classified as **Deferred** — prioritized core branch-aware cart and transactional reservation.
- `SD-002 (Comparison beyond 4 products)`: Classified as **Out of Scope** — constrained comparison to 2–4 items to maintain optimal mobile responsiveness.
- `SD-003 (Real-time Shipper GPS Tracking)`: Classified as **Out of Scope** — order fulfillment is modeled through standard delivery milestone status transitions.
- `SD-004 (Installation & Warranty Management)`: Classified as **Out of Scope** — retail appliances are covered under standard manufacturer documentation.
- `SD-005 (Guest Checkout without Account)`: Classified as **Deferred** — mandatory account registration enforces customer ownership and verified order history.

---

## 1.5. Technology Stack Summary

```text
┌──────────────────────────────────────────────────────────────────────────────────┐
│                             PRESENTATION TIER                                    │
│   React 19 • TypeScript • Vite 7 • Tailwind CSS / Modern Vanilla CSS • Lucide UI │
└────────────────────────────────────────┬─────────────────────────────────────────┘
                                         │ RESTful HTTP / JSON (Axios, OpenAPI 3.1)
┌────────────────────────────────────────▼─────────────────────────────────────────┐
│                             APPLICATION & API TIER                               │
│        ASP.NET Core Minimal APIs (.NET 10) • JWT Bearer Authentication           │
│        Clean Architecture • FluentValidation • MediatR / Service Handlers        │
└────────────────────────────────────────┬─────────────────────────────────────────┘
                                         │ Repository & Unit of Work / Invariants
┌────────────────────────────────────────▼─────────────────────────────────────────┐
│                           DOMAIN & PERSISTENCE TIER                              │
│       Entity Framework Core 10 • Pomelo / MySqlConnector • Transaction Scopes    │
│       MySQL 8.4 LTS (23 Relational Tables, Snake_case, UUID v7, Check Invariants)│
└──────────────────────────────────────────────────────────────────────────────────┘
```
