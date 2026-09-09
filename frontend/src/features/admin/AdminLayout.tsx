import { NavLink, Outlet } from 'react-router-dom'
import './AdminLayout.css'
import './AdminCatalog.css'

interface NavItem {
  to: string
  label: string
  icon: string
}

const NAV_ITEMS: NavItem[] = [
  { to: '/admin/dashboard', label: 'Tổng quan', icon: '📊' },
  { to: '/admin/orders', label: 'Đơn hàng', icon: '🧾' },
  { to: '/admin/branches', label: 'Chi nhánh', icon: '🏬' },
  { to: '/admin/inventory', label: 'Kho & Giá', icon: '📦' },
  { to: '/admin/forecast', label: 'Dự báo nhu cầu', icon: '📈' },
  { to: '/admin/recommendations', label: 'Gợi ý sản phẩm', icon: '✨' },
  { to: '/admin/promotions', label: 'Khuyến mãi', icon: '🎟️' },
  { to: '/admin/users', label: 'Người dùng', icon: '👥' },
  { to: '/admin/catalog/categories', label: 'Danh mục', icon: '🗂️' },
  { to: '/admin/catalog/brands', label: 'Thương hiệu', icon: '🏷️' },
  { to: '/admin/catalog/products', label: 'Sản phẩm', icon: '🛒' },
]

export function AdminLayout() {
  return (
    <div className="admin-shell">
      <aside className="admin-sidebar" aria-label="Điều hướng quản trị">
        <div className="admin-sidebar-brand">Bảng điều khiển</div>
        <nav className="admin-nav">
          {NAV_ITEMS.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) => `admin-nav-link ${isActive ? 'admin-nav-link--active' : ''}`}
            >
              <span className="admin-nav-icon" aria-hidden="true">{item.icon}</span>
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>
      </aside>
      <main className="admin-content">
        <Outlet />
      </main>
    </div>
  )
}
