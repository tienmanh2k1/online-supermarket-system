================================================================================
APTECHMART - MULTI BRANCH ONLINE SUPERMARKET SYSTEM
eProject (Project 3) Submission Package - Working Application
================================================================================

1. PROJECT OVERVIEW
--------------------------------------------------------------------------------
Project Title   : AptechMart Multi Branch Online Supermarket System
Batch / Class   : C2209L / CP2026
Institution     : FPT Aptech Computer Education
Program         : HDSE (Higher Diploma in Software Engineering)
Academic Period : June 2026 - September 2026

Student Information:
  - Student1328327 : MINH NGUYEN QUANG (Team Leader & Backend Lead)
  - Student1520086 : DUNG PHAM HUU     (Security & Identity Specialist)
  - Student1226001 : MANH MAI TIEN     (Transaction & Payment Engineer)
  - Student1242500 : LUONG NGUYEN DUC  (Frontend Lead & QA)

Technology Stack:
  - Backend  : ASP.NET Core Minimal APIs (.NET 10), EF Core 10, Clean Architecture
  - Frontend : React 19, TypeScript, Vite 7, Modern Responsive Design
  - Database : MySQL 8.4 LTS (23 Tables, Relational Schema, Snake_case, UUID v7)
  - Deployment: Multi-container Docker Compose (Isolated Network & Volumes)


2. TEST ACCOUNTS & SEED DATA
--------------------------------------------------------------------------------
The database is automatically pre-seeded with 3 supermarket physical branches,
standard product categories, brands, electronics & grocery products,
branch-specific inventory with independent pricing, and the following accounts:

+----------------------+--------------------+-----------+----------------------------------------------+
| Role                 | Email              | Password  | Core Testing Responsibilities                |
+----------------------+--------------------+-----------+----------------------------------------------+
| Administrator (Admin)| admin@test.com     | Test@123  | Full catalog CRUD, branch pricing & inventory|
|                      |                    |           | updates, order state transitions, user bans. |
+----------------------+--------------------+-----------+----------------------------------------------+
| Customer 1           | user1@test.com     | Test@123  | Branch selection, catalog browse, cart,      |
|                      |                    |           | transactional checkout, COD/VNPay sandbox.   |
+----------------------+--------------------+-----------+----------------------------------------------+
| Customer 2           | user2@test.com     | Test@123  | Verify isolated cart and order history       |
|                      |                    |           | between different registered customers.      |
+----------------------+--------------------+-----------+----------------------------------------------+
| Guest (Visitor)      | (No login needed)  | (None)    | Browse catalog, filter by price/category,    |
|                      |                    |           | switch branches to inspect localized stock.  |
+----------------------+--------------------+-----------+----------------------------------------------+


3. RECOMMENDED EXECUTION: DOCKER COMPOSE (SINGLE COMMAND)
--------------------------------------------------------------------------------
Prerequisites: Docker Desktop installed and running.

Step 1: Open a terminal in the root of the extracted source code folder.
Step 2: Run the following command:

    docker compose up --build

Docker will start 3 independent containers:
  1. db       : MySQL 8.4 LTS running on port 3306.
  2. api      : ASP.NET Core Minimal API (.NET 10) running on port 8080.
  3. frontend : React 19 Storefront (Nginx) running on port 5173.

Live Application Endpoints:
  - Storefront Web Interface : http://localhost:5173
  - Swagger / OpenAPI Doc    : http://localhost:8080/swagger
  - OpenApi Schema JSON      : http://localhost:8080/openapi/v1.json
  - System Health Check API  : http://localhost:8080/api/health

To stop and tear down containers:
    docker compose down


4. NATIVE EXECUTION (LOCAL DEVELOPMENT SETUP)
--------------------------------------------------------------------------------
Prerequisites:
  - .NET SDK 10.0 or later
  - Node.js 20+ LTS / Node.js 22+
  - MySQL Server 8.4 LTS running locally on port 3306

Step 1: Database Migration & Schema Creation
    Configure your connection string environment variable:
        PowerShell:
            $env:ConnectionStrings__DefaultConnection = "Server=localhost;Port=3306;Database=online_supermarket;User=root;Password=your_password;"
        Bash:
            export ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=online_supermarket;User=root;Password=your_password;"

    Apply EF Core migrations:
        dotnet ef database update --project backend/src/OnlineSupermarket.Infrastructure --startup-project backend/src/OnlineSupermarket.Api

Step 2: Run Backend API
    dotnet run --project backend/src/OnlineSupermarket.Api
    API will listen on http://localhost:5072 (or http://localhost:8080).

Step 3: Run Frontend Storefront Client
    In a separate terminal:
        cd frontend
        npm install
        npm run dev
    Storefront will open at http://localhost:5173.


5. RUNNING FROM COMPILED ARTIFACTS (b_Compiled_Code)
--------------------------------------------------------------------------------
If you prefer to run the pre-compiled binaries directly:

A. Backend:
   Directory: b_Compiled_Code/Backend/
   Prerequisites: .NET 10 Runtime installed.
   Command:
       dotnet OnlineSupermarket.Api.dll
   Or execute directly on Windows:
       .\OnlineSupermarket.Api.exe

B. Frontend:
   Directory: b_Compiled_Code/Frontend/
   The directory contains the static production bundle (HTML, CSS, JS).
   You can serve it with any web server (e.g. Nginx, IIS, or `npx serve .`).


6. AUTOMATED TEST SUITES
--------------------------------------------------------------------------------
To run the automated test suite across the solution:

Backend Tests (.NET 10):
    dotnet test backend/tests/OnlineSupermarket.Domain.Tests
    dotnet test backend/tests/OnlineSupermarket.Infrastructure.Tests
    dotnet test backend/tests/OnlineSupermarket.Api.Tests

Frontend Tests (Vitest):
    cd frontend
    npm run test:run

================================================================================
AptechMart Development Team - Academic Year 2026
================================================================================
