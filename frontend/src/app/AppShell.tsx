import type { PropsWithChildren } from 'react'
import { Link } from 'react-router-dom'
import { ApiStatus } from '../features/system/ApiStatus'
import { UserMenu } from '../features/auth/UserMenu'
import { CartHeaderLink } from '../features/cart/CartHeaderLink'
import { CompareHeaderLink } from '../features/compare/CompareHeaderLink'
import { BranchNavMenu } from '../features/products/BranchNavMenu'

export function AppShell({ children }: PropsWithChildren) {
  return (
    <div className="site-shell bg-slate-50 min-h-screen">
      <header className="site-header-wrap w-full bg-white border-b border-slate-200 sticky top-0 z-50 shadow-sm">
        <div className="site-header max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex items-center justify-between gap-6">
          <div className="flex items-center gap-6 lg:gap-8">
            <Link className="brand" to="/" aria-label="AptechMart — trang chủ">
              <span className="brand__mark">AM</span>
              <span>
                <strong>AptechMart</strong>
                <small>Siêu thị điện tử</small>
              </span>
            </Link>

            <nav className="site-nav flex items-center gap-6 lg:gap-8" aria-label="Điều hướng chính">
              <Link to="/browse" className="nav-menu-link">Sản phẩm</Link>
              <BranchNavMenu />
              <CompareHeaderLink />
              <CartHeaderLink />
            </nav>
          </div>

          <div className="header-actions flex items-center gap-3 md:gap-4">
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
