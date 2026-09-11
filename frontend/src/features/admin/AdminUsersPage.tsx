import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { adminApi, type PaginatedUsersDto, type UserSummaryDto } from '../../api/adminApi'
import { ApiError } from '../../api/httpClient'
import { AdminCard, AdminBadge, AdminPagination, AdminEmptyState } from './components'
import './AdminOrdersPage.css'
import './AdminInventoryPage.css'
import './AdminUsersPage.css'
import './AdminDesignSystem.css'

const PAGE_SIZE = 20

const USER_STATUSES = ['Active', 'Locked', 'Disabled'] as const

const USER_STATUS_LABELS: Record<string, string> = {
  Active: 'Đang hoạt động',
  Locked: 'Đã khóa',
  Disabled: 'Vô hiệu hóa',
}

const ROLE_LABELS: Record<string, string> = {
  Admin: 'Quản trị viên',
  Customer: 'Khách hàng',
}

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === 'AbortError'
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium' }).format(new Date(value))
}

function userStatusLabel(status: string) {
  return USER_STATUS_LABELS[status] ?? status
}

export function AdminUsersPage() {
  const { accessToken } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const page = Math.max(1, Number(searchParams.get('page') ?? '1') || 1)

  const [data, setData] = useState<PaginatedUsersDto | null>(null)
  const [loadState, setLoadState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [retryKey, setRetryKey] = useState(0)
  const [pending, setPending] = useState<{ user: UserSummaryDto; newStatus: string } | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoadState('loading')
    adminApi
      .listUsers(page, PAGE_SIZE, accessToken, controller.signal)
      .then((result) => {
        setData(result)
        setLoadState('ready')
      })
      .catch((error) => {
        if (!isAbortError(error)) {
          setData(null)
          setLoadState('error')
        }
      })
    return () => controller.abort()
  }, [accessToken, page, retryKey])

  function setPage(nextPage: number) {
    const next = new URLSearchParams(searchParams)
    next.set('page', String(nextPage))
    setSearchParams(next)
  }

  function requestChange(user: UserSummaryDto, newStatus: string) {
    if (newStatus === user.status) return
    setActionError(null)
    setPending({ user, newStatus })
  }

  function cancelChange() {
    setPending(null)
    setActionError(null)
  }

  async function confirmChange() {
    if (!pending || !accessToken) return
    setSubmitting(true)
    setActionError(null)
    try {
      await adminApi.updateUserStatus(pending.user.id, pending.newStatus, accessToken)
      setData((prev) =>
        prev
          ? {
              ...prev,
              users: prev.users.map((u) =>
                u.id === pending.user.id ? { ...u, status: pending.newStatus } : u,
              ),
            }
          : prev,
      )
      setPending(null)
    } catch (err) {
      if (err instanceof ApiError && err.data?.message) {
        setActionError(err.data.message)
      } else {
        setActionError('Không thể cập nhật trạng thái. Vui lòng thử lại.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  const totalPages = data ? Math.ceil(data.totalCount / PAGE_SIZE) : 1

  return (
    <main className="admin-page admin-users" role="main" aria-label="Quản lý người dùng">
      <header className="admin-page-header">
        <h1>Quản lý người dùng</h1>
        <p className="admin-page-sub">Xem danh sách tài khoản và khóa / mở khóa / vô hiệu hóa.</p>
      </header>

      <AdminCard
        toolbar={
          <div className="admin-toolbar-wrap flex items-center justify-between w-full">
            <h2 className="text-base font-semibold text-slate-800" style={{ margin: 0 }}>Danh sách người dùng</h2>
            {data && (
              <AdminBadge variant="neutral">
                Tổng: <strong style={{ marginLeft: 4 }}>{data.totalCount}</strong> tài khoản
              </AdminBadge>
            )}
          </div>
        }
        footer={
          data ? (
            <AdminPagination
              currentPage={page}
              totalPages={totalPages}
              totalCount={data.totalCount}
              itemName="người dùng"
              onPageChange={setPage}
            />
          ) : undefined
        }
      >
        {loadState === 'error' && (
          <section className="admin-alert p-6" role="alert">
            <p className="text-rose-600 font-medium">Không thể tải danh sách người dùng. Vui lòng thử lại.</p>
            <button type="button" className="btn btn-sm btn-secondary mt-2" onClick={() => setRetryKey((prev) => prev + 1)}>Thử lại</button>
          </section>
        )}

        {loadState === 'loading' && <p className="admin-loading py-8 text-center text-slate-500" aria-busy="true">Đang tải người dùng...</p>}

        {loadState === 'ready' && data && data.users.length === 0 && (
          <AdminEmptyState icon="👥" message="Không có người dùng nào." />
        )}

        {loadState === 'ready' && data && data.users.length > 0 && (
          <table className="admin-table admin-ds-table" aria-label="Danh sách người dùng" style={{ width: '100%' }}>
            <thead>
              <tr>
                <th scope="col" className="col-text text-left">Email</th>
                <th scope="col" className="col-text text-left">Họ tên</th>
                <th scope="col" className="col-text text-left">SĐT</th>
                <th scope="col" className="col-status text-center">Vai trò</th>
                <th scope="col" className="col-status text-center">Trạng thái</th>
                <th scope="col" className="col-text text-left">Ngày tạo</th>
                <th scope="col" className="col-actions text-right">Đổi trạng thái</th>
              </tr>
            </thead>
            <tbody>
              {data.users.map((user) => (
                <tr key={user.id}>
                  <td className="col-text text-left align-middle font-medium text-slate-800">{user.email}</td>
                  <td className="col-text text-left align-middle text-slate-700">{user.fullName}</td>
                  <td className="col-text text-left align-middle text-slate-600 font-mono text-xs">{user.phone || '—'}</td>
                  <td className="col-status text-center align-middle">
                    <AdminBadge variant={user.role === 'Admin' ? 'info' : 'neutral'} className={`admin-role admin-role--${user.role.toLowerCase()}`}>
                      {ROLE_LABELS[user.role] ?? user.role}
                    </AdminBadge>
                  </td>
                  <td className="col-status text-center align-middle">
                    <AdminBadge
                      variant={user.status === 'Active' ? 'success' : user.status === 'Locked' ? 'warning' : 'danger'}
                      dot
                      className={`admin-status admin-status--${user.status.toLowerCase()}`}
                    >
                      {userStatusLabel(user.status)}
                      <span className="sr-only">{user.status}</span>
                    </AdminBadge>
                  </td>
                  <td className="col-text text-left align-middle text-slate-600 text-xs">{formatDate(user.createdAtUtc)}</td>
                  <td className="col-actions text-right align-middle">
                    <select
                      value={user.status}
                      aria-label={`Đổi trạng thái ${user.email}`}
                      className="admin-select-filter text-xs py-1 px-2"
                      onChange={(event) => requestChange(user, event.target.value)}
                    >
                      {USER_STATUSES.map((status) => (
                        <option key={status} value={status}>{USER_STATUS_LABELS[status]}</option>
                      ))}
                    </select>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </AdminCard>

      {pending && (
        <div className="admin-modal-overlay" onClick={cancelChange}>
          <div
            className="admin-modal"
            role="dialog"
            aria-modal="true"
            aria-label="Xác nhận đổi trạng thái"
            onClick={(event) => event.stopPropagation()}
          >
            <header className="admin-modal-header">
              <h2>Xác nhận</h2>
              <button type="button" className="admin-modal-close" onClick={cancelChange} aria-label="Đóng">×</button>
            </header>
            <p>
              Đổi trạng thái tài khoản <strong>{pending.user.email}</strong> từ
              {' '}"{userStatusLabel(pending.user.status)}" sang "{userStatusLabel(pending.newStatus)}"?
            </p>
            {pending.newStatus !== 'Active' && (
              <p className="admin-warning" role="alert">
                Người dùng sẽ không thể đăng nhập khi tài khoản bị khóa hoặc vô hiệu hóa.
              </p>
            )}
            {actionError && <p className="admin-error" role="alert">{actionError}</p>}
            <div className="admin-modal-actions">
              <button type="button" className="admin-btn-secondary" onClick={cancelChange} disabled={submitting}>Hủy</button>
              <button type="button" className="btn-primary" onClick={confirmChange} disabled={submitting}>
                {submitting ? 'Đang lưu...' : 'Xác nhận'}
              </button>
            </div>
          </div>
        </div>
      )}
    </main>
  )
}
