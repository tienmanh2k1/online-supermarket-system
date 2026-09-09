# CHAPTER 6: USER GUIDE

This User Guide provides step-by-step, numbered operational instructions for the two primary user groups of the **AptechMart Multi Branch Online Supermarket System**: **Customers** and **System Administrators**.

All instructions follow the explicit operational format:  
**Navigation $\rightarrow$ Input / Selection $\rightarrow$ Action Button $\rightarrow$ Expected Outcome**.

---

## PART A: CUSTOMER OPERATIONAL GUIDE

### 1. Account Registration & Authentication
1. **Register a New Account**:
   - **Navigate**: Open browser to `http://localhost:5173` and click the **"Register"** button located at the top right of the navigation header.
   - **Input**: Enter your **Full Name** (e.g. *John Doe*), **Email Address** (e.g. *john@example.com*), **Phone Number** (e.g. *0912345678*), and **Password** (minimum 8 characters with upper, lower, and numeric characters).
   - **Action**: Click the **"Create Account"** button.
   - **Expected Outcome**: The system validates inputs, hashes credentials, displays a *"Registration Successful"* toast message, and automatically redirects to the Login page.
2. **Log In to System**:
   - **Navigate**: Click the **"Login"** button on the header.
   - **Input**: Enter your registered **Email** and **Password** (or use demo credentials: `user1@test.com` / `Test@123`).
   - **Action**: Click the **"Sign In"** button.
   - **Expected Outcome**: The header updates with your personal name badge and account navigation menu.
3. **Profile & Password Management**:
   - **Navigate**: Click your name on the header $\rightarrow$ select **"My Profile"**.
   - **Action**: Update contact information and click **"Save Changes"**. To update password, switch to the **"Change Password"** tab, enter current and new passwords, and click **"Update Password"**.
   - **Expected Outcome**: Profile and security credentials updated with confirmation message.

---

### 2. Multi-Point Delivery Address Book
1. **Navigate**: Go to **"My Profile"** $\rightarrow$ select the **"Address Book"** tab.
2. **Action**: Click the **"Add New Address"** button.
3. **Input**: Fill in **Recipient Name**, **Phone Number**, **Street Address**, **District**, and **City / Province**.
4. **Selection**: Check the box **"Set as Default Address"** if this is your primary shipping location.
5. **Action**: Click the **"Save Address"** button.
6. **Expected Outcome**: The address appears in your saved address list with an active *"Default"* badge.

---

### 3. Supermarket Branch Selection & Localized Inventory Inspection
1. **Navigate**: Click the **Branch Selector Dropdown** on the top navigation bar (e.g. showing *[Active Store: Cau Giay Branch]*).
2. **Selection**: Choose your preferred local branch from the list:
   - *Cau Giay Branch - 123 Cau Giay Street, Hanoi*
   - *Dong Da Branch - 456 Xa Dan Street, Hanoi*
   - *Ha Dong Branch - 789 Quang Trung Street, Hanoi*
3. **Expected Outcome**: The page immediately updates. Product cards, unit prices, and stock availability instantly refresh to reflect the chosen physical store.

---

### 4. Catalog Browsing, Search & Side-by-Side Comparison
1. **Keyword Search**:
   - **Navigate**: Click the search input on the top header.
   - **Input**: Type a product name or keyword (e.g. *Samsung*, *Inverter*, *OLED*) and press **Enter**.
   - **Expected Outcome**: Catalog results filter to items matching your keyword.
2. **Multi-Facet Filtering**:
   - **Navigate**: In the `/products` catalog view, locate the left sidebar filter.
   - **Selection**: Check category boxes (e.g. *Refrigerators*, *Washing Machines*), brand checkboxes (e.g. *LG*, *Samsung*), or adjust the price slider.
   - **Expected Outcome**: Catalog grid updates dynamically.
3. **Product Comparison (FR-104)**:
   - **Action**: On any product card, click the **"Compare"** icon button.
   - **Action**: Find another product **in the same category** and click its **"Compare"** icon.
   - **Expected Outcome**: A Comparison Modal opens showing technical attributes, prices, and stock side by side. Click **"Add to Cart"** or **"Close"**.

---

### 5. Shopping Cart & Quantity Validation
1. **Add Item to Cart**:
   - **Navigate**: Open a product detail page.
   - **Selection**: Adjust the quantity counter using the **"+"** or **"-"** buttons (the selector will not allow exceeding available branch stock).
   - **Action**: Click the **"Add to Cart"** button.
   - **Expected Outcome**: The header cart badge count increments and a confirmation popup appears.
2. **Review Shopping Cart**:
   - **Navigate**: Click the **"Shopping Cart"** icon in the header.
   - **Action**: Modify quantities or click **"Remove"** on unwanted items.
   - **Expected Outcome**: Cart subtotal recalculates immediately.

---

### 6. Transactional Checkout & Payment Execution
1. **Navigate**: In the cart screen, click the **"Proceed to Checkout"** button.
2. **Fulfillment Selection**:
   - Select **"Home Delivery"** (choose a saved shipping address) OR select **"Store Pickup"** (collect in person at the selected branch).
3. **Promotional Coupon**:
   - **Input**: Enter a promo code (e.g. `APTECH10`) into the **"Discount Code"** field.
   - **Action**: Click **"Apply"**.
   - **Expected Outcome**: Order summary shows the deducted discount amount.
4. **Payment Method Selection**:
   - Choose **"Cash on Delivery (COD)"**, **"VNPay Sandbox"**, or **"MoMo Sandbox"**.
5. **Action**: Click the **"Place Order"** button.
6. **Payment Redirection (for VNPay/MoMo)**:
   - Complete sandbox test verification on the gateway screen.
   - System receives the IPN callback and redirects to the Order Success receipt page.
7. **Expected Outcome**: Order created in `Processing` or `Confirmed` status with a unique order tracking number (e.g. `ORD-2026-0001`).

---

### 7. Order Tracking & Verified Product Reviews
1. **Track Order Progress**:
   - **Navigate**: Go to **"My Profile"** $\rightarrow$ select **"Order History"**.
   - **Action**: Click **"View Details"** on an active order.
   - **Expected Outcome**: Timeline displays current state: `Pending` $\rightarrow$ `Confirmed` $\rightarrow$ `Processing` $\rightarrow$ `Shipped` $\rightarrow$ `Completed`.
2. **Submit Verified Product Review**:
   - **Condition**: Order status must be **Completed**.
   - **Action**: Click **"Write a Review"** next to a delivered item.
   - **Input**: Select a 1–5 star rating and enter review text.
   - **Action**: Click **"Submit Review"**.
   - **Expected Outcome**: Review published with a *"Verified Purchase"* badge.

---

## PART B: ADMINISTRATOR OPERATIONAL GUIDE

### 1. Administrative Authentication
1. **Navigate**: Open browser to `http://localhost:5173/login`.
2. **Input**: Enter Admin credentials (`admin@test.com` / `Test@123`).
3. **Action**: Click **"Sign In"**.
4. **Expected Outcome**: System recognizes administrative role and reveals the **"Admin Portal"** link in the navigation menu.

---

### 2. Category & Brand Portfolio Management
1. **Category Tree Management**:
   - **Navigate**: Click **"Admin Portal"** $\rightarrow$ select **"Categories"** (`/admin/categories`).
   - **Action**: Click **"Add Category"**.
   - **Input**: Enter **Category Name**, **Slug**, choose **Parent Category** (for subcategories), and set **Display Order**.
   - **Action**: Click **"Save Category"**.
   - **Expected Outcome**: New category immediately appears in storefront navigation.
2. **Brand Management**:
   - **Navigate**: Select **"Brands"** (`/admin/brands`).
   - **Action**: Click **"Add Brand"**, input brand name and logo URL, then click **"Save"**.

---

### 3. Product Catalog & SKU Management
1. **Create New Product**:
   - **Navigate**: Select **"Products"** (`/admin/products`).
   - **Action**: Click the **"Create Product"** button.
   - **Input**: Enter **Product Name**, **Global SKU Code** (must be unique), select **Category** and **Brand**, enter **Unit of Measure**, and input technical specifications.
   - **Action**: Click **"Save Product"**.
   - **Expected Outcome**: Product record created globally and is now eligible for branch stocking.

---

### 4. Branch Inventory Replenishment & Price Control
1. **Navigate**: Select **"Branch Inventory"** (`/admin/inventory`).
2. **Selection**: Choose a target physical branch (e.g. *Cau Giay Branch*) from the dropdown.
3. **Action**: Locate the product and click **"Adjust Stock & Price"**.
4. **Input**: Enter the new **Selling Price (VND)**, updated **Quantity on Hand**, and **Reorder Level Threshold**.
5. **Action**: Click **"Update Inventory"**.
6. **Expected Outcome**: Stock levels and localized prices immediately take effect in the storefront for that branch.

---

### 5. Order Fulfillment & State Transition Operations
1. **Navigate**: Select **"Orders"** (`/admin/orders`).
2. **Selection**: Filter orders by status (e.g. `Confirmed`, `Processing`).
3. **Action**: Click on an order to open the **Order Details Modal**.
4. **State Transition**:
   - Click **"Mark as Processing"** when store staff begin packing items.
   - Click **"Dispatch / Shipped"** when handed to delivery drivers.
   - Click **"Mark as Completed"** when delivery is successfully fulfilled.
5. **Expected Outcome**: Order state transitions; stock is deducted permanently upon completion; customer tracking updates in real time.

---

### 6. User Governance & Access Suspension
1. **Navigate**: Select **"Users"** (`/admin/users`).
2. **Action**: Locate customer account using the search filter.
3. **Action**: Click the **"Lock Account"** button.
4. **Expected Outcome**: User account status toggles to `Locked`; all active JWT refresh tokens are immediately revoked, barring access.

---

### 7. Sales Analytics & Replenishment Intelligence
1. **Sales Performance Dashboard**:
   - **Navigate**: Select **"Reports"** $\rightarrow$ **"Sales Analytics"** (`/admin/reports/sales`).
   - **Selection**: Filter by date range (e.g. *Last 30 Days*) and optional branch filter.
   - **Expected Outcome**: Financial KPIs and charts render revenue, order volume, and average order value.
2. **Demand Forecast & Stock Replenishment Alerts**:
   - **Navigate**: Select **"Inventory Forecast"** (`/admin/forecast`).
   - **Expected Outcome**: System highlights products whose current available stock is below the designated reorder threshold, prompting stock replenishment.
