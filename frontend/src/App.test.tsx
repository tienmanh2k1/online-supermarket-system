import type { PropsWithChildren } from 'react'
import { render, screen } from '@testing-library/react'
import { expect, it, vi } from 'vitest'
import App from './App'

vi.mock('./app/AppShell', () => ({
  AppShell: ({ children }: PropsWithChildren) => <main>{children}</main>,
}))
vi.mock('./features/auth/AuthContext', () => ({
  AuthProvider: ({ children }: PropsWithChildren) => <>{children}</>,
}))
vi.mock('./features/cart/CartContext', () => ({
  CartProvider: ({ children }: PropsWithChildren) => <>{children}</>,
}))
vi.mock('./features/products/ProductBrowsePage', () => ({ ProductBrowsePage: () => <div /> }))
vi.mock('./features/products/ProductDetailPage', () => ({ ProductDetailPage: () => <div /> }))
vi.mock('./features/account/ProfilePage', () => ({ ProfilePage: () => <div /> }))
vi.mock('./features/account/AddressListPage', () => ({ AddressListPage: () => <div /> }))
vi.mock('./features/cart/CartPage', () => ({ CartPage: () => <div /> }))
vi.mock('./features/checkout/CheckoutPage', () => ({ CheckoutPage: () => <div>Checkout Page Route</div> }))
vi.mock('./features/checkout/CheckoutSuccessPage', () => ({
  CheckoutSuccessPage: () => <h1>Checkout success route</h1>,
}))
vi.mock('./features/orders/OrderHistoryPage', () => ({
  OrderHistoryPage: () => <h1>Order history route</h1>,
}))
vi.mock('./features/orders/OrderDetailPage', () => ({
  OrderDetailPage: () => <div>Order detail route</div>,
}))

vi.mock('./features/admin/AdminRoute', async () => {
  const { Outlet } = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')
  return {
    AdminRoute: () => <Outlet />,
  }
})
vi.mock('./features/admin/AdminLayout', async () => {
  const { Outlet } = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')
  return {
    AdminLayout: () => (
      <div>
        <p>AdminLayout Route</p>
        <Outlet />
      </div>
    ),
  }
})
vi.mock('./features/admin/categories/AdminCategoriesPage', () => ({
  AdminCategoriesPage: () => <h1>Admin Categories Route</h1>,
}))
vi.mock('./features/admin/brands/AdminBrandsPage', () => ({
  AdminBrandsPage: () => <h1>Admin Brands Route</h1>,
}))
vi.mock('./features/admin/products/AdminProductsPage', () => ({
  AdminProductsPage: () => <h1>Admin Products Route</h1>,
}))
vi.mock('./features/admin/AdminInventoryPage', () => ({
  AdminInventoryPage: () => <h1>Admin Inventory Route</h1>,
}))
vi.mock('./features/admin/AdminForecastPage', () => ({
  AdminForecastPage: () => <h1>Admin Forecast Route</h1>,
}))

it('wires the admin categories route', () => {
  window.history.pushState({}, '', '/admin/catalog/categories')
  render(<App />)
  expect(screen.getByRole('heading', { name: 'Admin Categories Route' })).toBeInTheDocument()
})

it('wires the admin brands route', () => {
  window.history.pushState({}, '', '/admin/catalog/brands')
  render(<App />)
  expect(screen.getByRole('heading', { name: 'Admin Brands Route' })).toBeInTheDocument()
})

it('wires the admin products route', () => {
  window.history.pushState({}, '', '/admin/catalog/products')
  render(<App />)
  expect(screen.getByRole('heading', { name: 'Admin Products Route' })).toBeInTheDocument()
})

it('redirects /admin to /admin/catalog/categories', () => {
  window.history.pushState({}, '', '/admin')
  render(<App />)
  expect(screen.getByRole('heading', { name: 'Admin Categories Route' })).toBeInTheDocument()
})

it('wires the admin inventory route', () => {
  window.history.pushState({}, '', '/admin/inventory')
  render(<App />)
  expect(screen.getByRole('heading', { name: 'Admin Inventory Route' })).toBeInTheDocument()
})

it('wires the admin forecast route', () => {
  window.history.pushState({}, '', '/admin/forecast')
  render(<App />)
  expect(screen.getByRole('heading', { name: 'Admin Forecast Route' })).toBeInTheDocument()
})


it('wires the checkout success route', () => {
  window.history.pushState(
    {},
    '',
    '/shopping/checkout/success?orderId=order-1&paymentStatus=PendingCollection'
  )
  render(<App />)
  expect(screen.getByRole('heading', { name: 'Checkout success route' })).toBeInTheDocument()
})

it('wires the checkout page route', () => {
  window.history.pushState({}, '', '/shopping/checkout')
  render(<App />)
  expect(screen.getByText('Checkout Page Route')).toBeInTheDocument()
})

it('wires the order history route', () => {
  window.history.pushState({}, '', '/orders/history')
  render(<App />)
  expect(screen.getByRole('heading', { name: 'Order history route' })).toBeInTheDocument()
})

it('wires the order detail route', () => {
  window.history.pushState({}, '', '/orders/history/00000000-0000-0000-0000-000000000001')
  render(<App />)
  expect(screen.getByText('Order detail route')).toBeInTheDocument()
})
