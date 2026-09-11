import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from './AuthContext'
import { AuthModal } from './AuthModal'

export function UserMenu() {
  const { user, isAuthenticated, isLoading, logout } = useAuth()
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [modalMode, setModalMode] = useState<'login' | 'register'>('login')

  const openAuth = (mode: 'login' | 'register') => {
    setModalMode(mode)
    setIsModalOpen(true)
  }

  if (isLoading) {
    return <div className="user-menu user-menu--loading"><span className="spinner" /></div>
  }

  if (isAuthenticated && user) {
    const initials = user.fullName
      .split(' ')
      .filter(Boolean)
      .map((part) => part[0])
      .join('')
      .slice(0, 2)
      .toUpperCase()

    return (
      <div className="user-menu user-menu--authenticated flex items-center gap-2 md:gap-2.5">
        <Link to="/account/profile" className="user-profile-link inline-flex items-center" title="Quản lý hồ sơ cá nhân">
          <div className="user-profile flex items-center gap-2">
            <div className="user-avatar" title={user.fullName}>
              {initials || 'U'}
            </div>
            <div className="user-info flex flex-col justify-center">
              <span className="user-name text-xs font-semibold leading-tight text-slate-800">{user.fullName}</span>
              <span className="user-role-badge text-[10px] font-medium leading-tight text-emerald-600">
                {user.role === 'Admin' ? 'Quản trị viên' : 'Khách hàng'}
              </span>
            </div>
          </div>
        </Link>
        <Link to="/account/addresses" className="btn-nav-address inline-flex items-center gap-1 text-xs" title="Sổ địa chỉ giao hàng">
          <span>📍</span>
          <span>Địa chỉ</span>
        </Link>
        <Link to="/orders/history" className="btn-nav-orders inline-flex items-center gap-1 text-xs" title="Lịch sử đơn hàng">
          <span>📦</span>
          <span>Đơn hàng</span>
        </Link>
        {user.role === 'Admin' && (
          <Link to="/admin/orders" className="btn-nav-admin inline-flex items-center gap-1 text-xs" title="Khu vực quản trị">
            <span>⚙️</span>
            <span>Quản trị</span>
          </Link>
        )}
        <button
          type="button"
          className="btn-logout inline-flex items-center text-xs"
          onClick={() => logout()}
          title="Đăng xuất khỏi hệ thống"
        >
          Đăng xuất
        </button>
      </div>
    )
  }

  return (
    <>
      <div className="user-menu user-menu--guest flex items-center gap-2">
        <button
          type="button"
          className="btn-login inline-flex items-center text-xs font-semibold"
          onClick={() => openAuth('login')}
        >
          Đăng nhập
        </button>
        <button
          type="button"
          className="btn-register inline-flex items-center text-xs font-semibold"
          onClick={() => openAuth('register')}
        >
          Đăng ký
        </button>
      </div>

      <AuthModal
        isOpen={isModalOpen}
        initialMode={modalMode}
        onClose={() => setIsModalOpen(false)}
      />
    </>
  )
}
