# CHAPTER 7: DEVELOPER'S GUIDE

This guide provides exhaustive technical documentation enabling any software engineer or technical evaluator to build, configure, run, test, and deploy the **AptechMart Multi Branch Online Supermarket System** from scratch.

---

## 7.1. Development Prerequisites & Environment Versions

To run the full solution natively or in containerized mode, verify the following prerequisites:

| Tool / Runtime | Minimum Version | Recommended Version | Verification Command |
| :--- | :--- | :--- | :--- |
| **.NET SDK** | 10.0.100+ | 10.0.400+ | `dotnet --version` |
| **Node.js** | 20.0.0 LTS | 22.0.0+ | `node --version` |
| **npm** | 10.0.0+ | 10.8.0+ | `npm --version` |
| **MySQL Server** | 8.0.36+ | 8.4.0 LTS | `mysql --version` |
| **Docker Desktop** | 24.0.0+ | 27.0.0+ | `docker --version` |
| **Docker Compose** | 2.20.0+ | 2.29.0+ | `docker compose version` |

---

## 7.2. Solution Directory Structure & Architectural Responsibilities

```text
online-supermarket-system/
├── backend/                                # .NET 10 Solution Root
│   ├── src/
│   │   ├── OnlineSupermarket.Domain/       # DOMAIN CORE: Entities, Aggregates, Enums, Invariants
│   │   │   ├── Common/                     # BaseEntity, DomainValidationException
│   │   │   ├── Inventory/                  # BranchInventory, Branch, InventoryTransaction
│   │   │   ├── Identity/                   # User, Address, RefreshToken
│   │   │   ├── Catalog/                    # Category, Brand, Product
│   │   │   ├── Cart/                       # Cart, CartItem
│   │   │   ├── Orders/                     # Order, OrderItem, OrderStatusHistory
│   │   │   └── Payments/                   # Payment, PaymentCallback
│   │   │
│   │   ├── OnlineSupermarket.Infrastructure/ # INFRASTRUCTURE: Data Access, External Services
│   │   │   ├── Persistence/                # AppDbContext, Entity Configurations, Migrations
│   │   │   │   └── DataSeeder.cs           # Automated seed data generator
│   │   │   ├── Security/                   # PasswordHasher (PBKDF2), TokenService (JWT)
│   │   │   ├── Payments/                   # VNPay, MoMo & COD gateway processors
│   │   │   └── Jobs/                       # BackgroundJobRunExecutor, JobErrorSanitizer
│   │   │
│   │   └── OnlineSupermarket.Api/          # PRESENTATION / API: Minimal APIs Endpoints
│   │       ├── Endpoints/                  # Auth, Products, Cart, Checkout, Admin, Reports
│   │       ├── Middleware/                 # ErrorHandling, SensitiveDataSanitizer
│   │       ├── appsettings.json            # Configuration template
│   │       └── Program.cs                  # Application bootstrap & dependency injection
│   │
│   └── tests/                              # AUTOMATED TEST SUITES
│       ├── OnlineSupermarket.Domain.Tests/         # Unit tests for domain models & invariants
│       ├── OnlineSupermarket.Infrastructure.Tests/ # EF Core, DB migrations & security tests
│       └── OnlineSupermarket.Api.Tests/            # Endpoint integration tests & RBAC
│
├── frontend/                               # React 19 Frontend Web Application
│   ├── src/
│   │   ├── api/                            # Axios API client modules (products, cart, checkout)
│   │   ├── components/                     # Reusable UI widgets (Header, Footer, ProductCard)
│   │   ├── context/                        # Global state providers (AuthContext, BranchContext, CartContext)
│   │   ├── pages/                          # Application routes (Home, Browse, Detail, Cart, Checkout, Admin)
│   │   ├── types/                          # TypeScript entity definitions
│   │   └── App.tsx                         # Router configuration
│   ├── package.json                        # Dependencies (Vite, React 19, Tailwind, Lucide)
│   └── vite.config.ts                      # Build configuration & proxy settings
│
├── compose.yaml                            # Multi-container Docker Compose configuration
├── .env.example                            # Environment variables blueprint
└── README.md                               # Project documentation
```

---

## 7.3. Environment Configuration (`.env.example`)

Before running the application, copy `.env.example` to create `.env` in the repository root:

```ini
# Database Connection String
ConnectionStrings__DefaultConnection=Server=localhost;Port=3306;Database=online_supermarket;User=root;Password=root_password;

# JWT Authentication Configuration
Jwt__SecretKey=SuperSecretKeyForAptechMartProject3MustBeAtLeast32BytesLong!
Jwt__Issuer=OnlineSupermarketApi
Jwt__Audience=OnlineSupermarketClient
Jwt__AccessTokenExpirationMinutes=15
Jwt__RefreshTokenExpirationDays=7

# Third-Party Payment Sandbox Credentials
Payment__VNPay__TmnCode=DEMO_TMN_CODE
Payment__VNPay__HashSecret=DEMO_HASH_SECRET
Payment__VNPay__BaseUrl=https://sandbox.vnpayment.vn/paymentv2/vpcpay.html
Payment__MoMo__PartnerCode=DEMO_MOMO_PARTNER
Payment__MoMo__AccessKey=DEMO_ACCESS_KEY
Payment__MoMo__SecretKey=DEMO_SECRET_KEY
```

---

## 7.4. Database Setup, Migrations & Data Seeding

### 1. Database Creation
Ensure MySQL 8.4 Server is running on port `3306`:
```sql
CREATE DATABASE IF NOT EXISTS online_supermarket 
CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

### 2. Executing Migrations
Apply all EF Core migrations to construct the 23 relational tables:
```powershell
dotnet ef database update \
  --project backend/src/OnlineSupermarket.Infrastructure \
  --startup-project backend/src/OnlineSupermarket.Api
```

### 3. Automated Data Seeding
Upon application startup, `DataSeeder.cs` automatically verifies if records exist. If empty, it seeds:
- 3 Physical Supermarket Branches (Cau Giay, Dong Da, Ha Dong).
- 4 Multi-tier Category trees (Electronics, Home Appliances, Groceries, Beverages).
- 8 Leading Manufacturer Brands (Samsung, LG, Sony, Panasonic, Apple, Unilever, Nestle, Vinamilk).
- 28 Products with SKU, technical specifications, and image URLs.
- Branch inventory allocations with localized prices, stock on hand, and reorder levels.
- Pre-configured user accounts:
  - Admin: `admin@test.com` / `Test@123`
  - Customer 1: `user1@test.com` / `Test@123`
  - Customer 2: `user2@test.com` / `Test@123`

---

## 7.5. Running the Application

### Method 1: Single-Command Docker Compose (Recommended)
From the repository root:
```bash
docker compose up --build
```
This starts:
- **MySQL Container**: Port `3306` (with healthcheck).
- **Backend Container**: Port `8080` (HTTP Minimal APIs).
- **Frontend Container**: Port `5173` (Nginx serving React 19).

Access Points:
- Frontend Storefront: `http://localhost:5173`
- Swagger / OpenAPI UI: `http://localhost:8080/swagger`
- System Health Check: `http://localhost:8080/api/health`

### Method 2: Native Execution for Development
1. **Start Backend**:
   ```powershell
   dotnet run --project backend/src/OnlineSupermarket.Api
   ```
   API listens on `http://localhost:5072` (or `http://localhost:8080`).

2. **Start Frontend**:
   ```powershell
   cd frontend
   npm install
   npm run dev
   ```
   Vite dev server opens at `http://localhost:5173`.

---

## 7.6. Build & Test Execution

### 1. Automated Testing Commands
To execute the automated regression test suite across all solution projects:

```powershell
# Run Domain Unit Tests (52 tests)
dotnet test backend/tests/OnlineSupermarket.Domain.Tests

# Run Infrastructure & Persistence Tests (46 tests)
dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests

# Run API Integration & Endpoint Tests (48 tests)
dotnet test backend/tests/OnlineSupermarket.Api.Tests

# Run Frontend Vitest Suite (38 tests)
cd frontend
npm run test:run
```

### 2. Compiling Release Artifacts
```powershell
# Publish Backend (.NET 10 Release)
dotnet publish backend/src/OnlineSupermarket.Api -c Release -o submission/I_Working_Application/b_Compiled_Code/Backend

# Build Frontend (Vite Production Bundle)
cd frontend
npm run build
# Output is located in frontend/dist/
```

---

## 7.7. Common Troubleshooting & Solutions

| Issue / Error | Root Cause | Solution / Fix |
| :--- | :--- | :--- |
| **Port 3306 or 8080 in use** | Existing local MySQL or web server already bound to port. | Stop conflicting service (`net stop mysql`) or adjust host port mapping in `compose.yaml`. |
| **DB connection failed on startup** | API container starts before MySQL finishes initialization. | `compose.yaml` includes `depends_on` with `condition: service_healthy`. For native dev, wait for MySQL service to start. |
| **409 Conflict during checkout** | Requested item quantity exceeds available stock (`on_hand - reserved`). | Expected business invariant behavior. Replenish inventory via Admin Portal or reduce cart quantity. |
| **401 Unauthorized on API calls** | JWT token expired (15-min lifetime). | Refresh token via `POST /api/auth/refresh` or log in again. |
| **CORS errors in browser console** | Frontend origin not present in API CORS whitelist. | Verify `appsettings.json` `Cors:AllowedOrigins` contains `http://localhost:5173`. |
