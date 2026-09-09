import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AppShell } from './app/AppShell'
import { AuthProvider } from './features/auth/AuthContext'
import { CartProvider } from './features/cart/CartContext'
import { CompareProvider } from './features/compare/CompareContext'
import { CompareModal } from './features/compare/CompareModal'
import { ProductBrowsePage } from './features/products/ProductBrowsePage'
import { ProductDetailPage } from './features/products/ProductDetailPage'
import { BranchesPage } from './features/products/BranchesPage'
import { ResetPasswordPage } from './features/auth/ResetPasswordPage'
import { ProfilePage } from './features/account/ProfilePage'
import { AddressListPage } from './features/account/AddressListPage'
import { CartPage } from './features/cart/CartPage'
import { CheckoutPage } from './features/checkout/CheckoutPage'
import { CheckoutSuccessPage } from './features/checkout/CheckoutSuccessPage'
import { OrderHistoryPage } from './features/orders/OrderHistoryPage'
import { OrderDetailPage } from './features/orders/OrderDetailPage'
import { AdminRoute } from './features/admin/AdminRoute'
import { AdminLayout } from './features/admin/AdminLayout'
import { AdminOrdersPage } from './features/admin/AdminOrdersPage'
import { AdminOrderDetailPage } from './features/admin/AdminOrderDetailPage'
import { AdminInventoryPage } from './features/admin/AdminInventoryPage'
import { AdminForecastPage } from './features/admin/AdminForecastPage'
import { AdminRecommendationsPage } from './features/admin/AdminRecommendationsPage'
import { AdminUsersPage } from './features/admin/AdminUsersPage'
import { AdminPromotionsPage } from './features/admin/AdminPromotionsPage'
import { AdminBranchesPage } from './features/admin/AdminBranchesPage'
import { AdminCategoriesPage } from './features/admin/categories/AdminCategoriesPage'
import { AdminBrandsPage } from './features/admin/brands/AdminBrandsPage'
import { AdminProductsPage } from './features/admin/products/AdminProductsPage'

function CompareAppShell() {
  return (
    <>
      <AppShell>
        <Routes>
          <Route path="/" element={<ProductBrowsePage />} />
          <Route path="/browse" element={<ProductBrowsePage />} />
          <Route path="/products" element={<ProductBrowsePage />} />
          <Route path="/product/:id" element={<ProductDetailPage />} />
          <Route path="/branches" element={<BranchesPage />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route path="/account/profile" element={<ProfilePage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/account/addresses" element={<AddressListPage />} />
          <Route path="/addresses" element={<AddressListPage />} />
          <Route path="/shopping/cart" element={<CartPage />} />
          <Route path="/shopping/checkout" element={<CheckoutPage />} />
          <Route path="/shopping/checkout/success" element={<CheckoutSuccessPage />} />
          <Route path="/orders/history" element={<OrderHistoryPage />} />
          <Route path="/orders/history/:id" element={<OrderDetailPage />} />

          <Route element={<AdminRoute />}>
            <Route path="/admin" element={<AdminLayout />}>
              <Route index element={<Navigate to="catalog/categories" replace />} />
              <Route path="orders" element={<AdminOrdersPage />} />
              <Route path="orders/:id" element={<AdminOrderDetailPage />} />
              <Route path="branches" element={<AdminBranchesPage />} />
              <Route path="inventory" element={<AdminInventoryPage />} />
              <Route path="forecast" element={<AdminForecastPage />} />
              <Route path="recommendations" element={<AdminRecommendationsPage />} />
              <Route path="promotions" element={<AdminPromotionsPage />} />
              <Route path="users" element={<AdminUsersPage />} />
              <Route path="catalog/categories" element={<AdminCategoriesPage />} />
              <Route path="catalog/brands" element={<AdminBrandsPage />} />
              <Route path="catalog/products" element={<AdminProductsPage />} />
            </Route>
          </Route>
        </Routes>
      </AppShell>
      <CompareModal />
    </>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <CartProvider>
          <CompareProvider>
            <CompareAppShell />
          </CompareProvider>
        </CartProvider>
      </AuthProvider>
    </BrowserRouter>
  )
}
