import type { PropsWithChildren } from 'react'
import { Link, NavLink } from 'react-router-dom'
import { ApiStatus } from '../features/system/ApiStatus'
import { UserMenu } from '../features/auth/UserMenu'
import { CartHeaderLink } from '../features/cart/CartHeaderLink'
import { CompareHeaderLink } from '../features/compare/CompareHeaderLink'
import { BranchNavMenu } from '../features/products/BranchNavMenu'

export function AppShell({ children }: PropsWithChildren) {
  return (
    <div className="site-shell bg-slate-50 min-h-screen">
      <header className="site-header-wrap w-full bg-white border-b border-slate-200 sticky top-0 z-50 shadow-sm">
        <div className="site-header max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex items-center justify-between gap-4 lg:gap-8 flex-nowrap">
          {/* Khối Trái: Logo */}
          <Link className="brand shrink-0 flex items-center gap-3" to="/" aria-label="AptechMart — trang chủ">
            <span className="brand__mark shrink-0">AM</span>
            <span className="brand__text flex flex-col justify-center">
              <strong>AptechMart</strong>
              <small>Siêu thị điện tử</small>
            </span>
          </Link>

          {/* Khối Giữa: Navigation */}
          <nav className="site-nav flex items-center gap-1 sm:gap-2 lg:gap-3 flex-nowrap" aria-label="Điều hướng chính">
            <NavLink
              to="/browse"
              className={({ isActive }) =>
                `nav-menu-link ${isActive ? 'nav-menu-link--active' : ''}`
              }
            >
              Sản phẩm
            </NavLink>
            <BranchNavMenu />
            <CompareHeaderLink />
            <CartHeaderLink />
          </nav>

          {/* Khối Phải: Actions */}
          <div className="header-actions shrink-0 flex items-center gap-2.5 sm:gap-3 lg:gap-4">
            <UserMenu />
            <ApiStatus />
          </div>
        </div>
      </header>
      <main id="top" className="site-main-content flex-1 py-6 md:py-8">{children}</main>
      <footer className="site-footer w-full bg-white border-t border-slate-200 mt-auto">
        <div className="site-footer__content max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <p className="site-footer__copyright">© 2026 AptechMart. Siêu thị điện tử tiện lợi.</p>
          <div className="site-footer__links">
            <Link to="/privacy">Chính sách bảo mật</Link>
            <span className="site-footer__sep" aria-hidden="true">•</span>
            <Link to="/terms">Điều khoản dịch vụ</Link>
          </div>
        </div>
      </footer>
    </div>
  )
}
