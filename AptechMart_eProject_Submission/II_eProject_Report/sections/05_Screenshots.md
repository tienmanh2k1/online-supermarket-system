# CHAPTER 4: SYSTEM SCREENSHOTS & UI WALKTHROUGH

This chapter provides a comprehensive visual walkthrough of the **AptechMart** system, documenting the user interface layout, visual components, interaction affordances, and real operating screens across both the **Storefront Client** and **Admin Management Portal**.

---

## 4.1. Customer Storefront Interface

### Figure 4.1: Storefront Homepage & Physical Branch Selector
![Storefront Homepage & Physical Branch Selector](images/Figure_4.1_Homepage_BranchSelector.png)

> **Figure 4.1 Storefront Homepage & Branch Selector**  
> This screen serves as the primary digital gateway for shoppers. The top navigation bar features the AptechMart brand identity, full-text catalog search, real-time cart badge counter, user account menu, and the prominent **Physical Branch Selector Dropdown**. Below the header, promotional banners highlight active campaigns, followed by localized best-selling categories and real-time product cards displaying branch-specific pricing and stock levels.

---

### Figure 4.2: Customer Authentication & Login Interface
![Customer Authentication & Login Interface](images/Figure_4.2_Login_Form.png)

> **Figure 4.2 Customer Login Interface**  
> This interface facilitates secure customer authentication. Customers input their registered email address and password. The system applies client-side validation rules before submitting credentials over HTTPS to the `/api/auth/login` endpoint, which verifies the PBKDF2 salted hash and returns short-lived JWT access tokens and persistent refresh tokens.

---

### Figure 4.3: Customer Authenticated State & Profile Dashboard
![Customer Authenticated State & Profile Dashboard](images/Figure_4.3_Authenticated_State.png)

> **Figure 4.3 Customer Authenticated State**  
> This screen demonstrates the user interface following successful authentication. The header reflects the customer's identity with personalized navigation controls. Customers can seamlessly toggle into their profile dashboard, multi-point shipping address book, and order tracking history without leaving the shopping context.

---

### Figure 4.4: Product Catalogue & Multi-Facet Filtering
![Product Catalogue & Multi-Facet Filtering](images/Figure_4.4_Catalog_Filtering.png)

> **Figure 4.4 Product Catalogue & Filtering System**  
> This screen allows customers to browse the supermarket inventory with multi-faceted filtering criteria. The left sidebar provides interactive controls to narrow results by multi-tier categories, manufacturer brands, and price sliders in Vietnamese Dong (VND). The central grid presents matching product cards showing localized unit prices and in-stock badges for the active store location.

---

### Figure 4.5: Product Specification Detail View
![Product Specification Detail View](images/Figure_4.5_Product_Detail.png)

> **Figure 4.5 Product Detail & Localized Stock View**  
> This screen provides comprehensive product details, high-resolution thumbnail galleries, full technical specifications, and real-time stock availability specific to the active branch (e.g. *"8 units available at Cau Giay Branch"*). Shoppers can select desired quantities up to the branch availability ceiling, add items to their localized cart, or launch product comparison.

---

### Figure 4.6: Side-by-Side Product Comparison Modal
![Side-by-Side Product Comparison Modal](images/Figure_4.6_Product_Comparison.png)

> **Figure 4.6 Product Comparison Modal**  
> This modal window provides a side-by-side technical specification comparison of 2 to 4 products within the same category (FR-104). The synchronized matrix aligns key technical attributes, prices, stock statuses, and user ratings, empowering customers to make informed purchasing choices.

---

### Figure 4.7: Branch-Aware Shopping Cart Management
![Branch-Aware Shopping Cart Management](images/Figure_4.7_Shopping_Cart.png)

> **Figure 4.7 Shopping Cart Management**  
> This screen manages the customer's shopping cart bound to the chosen supermarket branch. Customers can adjust item quantities, remove items, review subtotal breakdowns, and verify that all requested items remain in stock at the physical branch prior to proceeding to checkout.

---

### Figure 4.8: Transactional Checkout & Delivery Selection
![Transactional Checkout & Delivery Selection](images/Figure_4.8_Checkout_Delivery.png)

> **Figure 4.8 Checkout & Fulfillment Mode Selection**  
> This screen guides customers through the checkout process. Customers choose between "Home Delivery" (selecting a saved address from their address book) and "Store Pickup" (confirming in-person collection at the active branch). Promotional coupon codes can be entered to receive discounts, and final totals are computed in real time.

---

### Figure 4.9: Sandbox Payment Gateway Processing
![Sandbox Payment Gateway Processing](images/Figure_4.9_Payment_Gateway.png)

> **Figure 4.9 Payment Gateway Selection & Redirection**  
> This screen presents available payment options: Cash on Delivery (COD), VNPay Sandbox, and MoMo Sandbox. When an online gateway is selected, the system builds an HMAC-SHA512 tamper-proof redirection payload, directing the user to the gateway sandbox and awaiting an asynchronous, idempotent Webhook IPN callback.

---

### Figure 4.10: Order History & Real-Time Tracking
![Order History & Real-Time Tracking](images/Figure_4.10_Order_History.png)

> **Figure 4.10 Order History & Status Timeline**  
> This screen displays the customer's chronological order history. Detailed views render item snapshots (locked prices and product names at purchase time), fulfillment method, payment status, and a visual state transition timeline (`Pending` $\rightarrow$ `Confirmed` $\rightarrow$ `Processing` $\rightarrow$ `Shipped` $\rightarrow$ `Completed`).

---

## 4.2. Administrator Management Portal

### Figure 4.11: Admin Multi-Level Category & Brand Management
![Admin Multi-Level Category & Brand Management](images/Figure_4.11_Admin_Categories.png)

> **Figure 4.11 Admin Category & Brand Catalog**  
> This administrative interface allows store managers to organize the multi-tier category tree (parent categories and subcategories), assign SEO-friendly slugs, define display ordering, and curate the brand manufacturer directory.

---

### Figure 4.12: Admin Product Master & Global SKU Catalog
![Admin Product Master & Global SKU Catalog](images/Figure_4.12_Admin_Products.png)

> **Figure 4.12 Admin Product Catalog Management**  
> This screen allows administrators to create and maintain global product records. Managers define commercial names, unique SKU barcodes, units of measure, image galleries, and structured technical specifications stored as JSON attributes.

---

### Figure 4.13: Admin Branch Pricing & Stockroom Replenishment
![Admin Branch Pricing & Stockroom Replenishment](images/Figure_4.13_Admin_Inventory.png)

> **Figure 4.13 Branch-Specific Pricing & Inventory Management**  
> This operational screen allows store managers to set selling prices and adjust on-hand inventory levels (`quantity_on_hand`) independently for each supermarket branch. Input validation guards prevent setting on-hand stock lower than currently active reservations.

---

### Figure 4.14: Admin Order Management & State Transition Control
![Admin Order Management & State Transition Control](images/Figure_4.14_Admin_Orders.png)

> **Figure 4.14 Admin Order Processing Dashboard**  
> This screen provides store staff with an aggregated view of all orders across branches. Staff can filter by fulfillment status, inspect delivery addresses, view payment receipts, and advance order state transitions strictly following the domain state machine.

---

### Figure 4.15: Admin User Governance & Access Revocation
![Admin User Governance & Access Revocation](images/Figure_4.15_Admin_Users.png)

> **Figure 4.15 User Management & Account Lockout**  
> This security interface allows administrators to view all registered user accounts, inspect role assignments, and execute immediate account suspensions. Suspending an account instantly revokes all active refresh tokens, barring compromised users from accessing the system.

---

### Figure 4.16: Admin Sales Analytics & Demand Forecasting
![Admin Sales Analytics & Demand Forecasting](images/Figure_4.16_Admin_Reports.png)

> **Figure 4.16 Sales Analytics & Restocking Alerts**  
> This analytical dashboard displays gross sales metrics, order fulfillment counts, and category revenue breakdowns. Below the financial KPIs, automated demand forecasting algorithms generate replenishment alert warnings for inventory items falling below store reorder thresholds.
