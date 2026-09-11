import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { adminApi } from '../../api/adminApi'
import type { PaginatedOrdersDto } from '../../api/orderApi'
import { ORDER_STATUSES, formatStatus } from './orderStatus'
import { AdminCard, AdminBadge, AdminPagination, AdminEmptyState } from './components'
import './AdminOrdersPage.css'
import './AdminDesignSystem.css'

const PAGE_SIZE = 10

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === 'AbortError'
}

function formatPrice(value: number) {
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value)
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

function shortId(id: string) {
  return id.slice(0, 8)
}

export function AdminOrdersPage() {
  const { accessToken } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const page = Math.max(1, Number(searchParams.get('page') ?? '1') || 1)
  const status = searchParams.get('status') || ''
  const userId = searchParams.get('userId') || ''
  const [userIdInput, setUserIdInput] = useState(userId)
  const [orders, setOrders] = useState<PaginatedOrdersDto | null>(null)
  const [loadState, setLoadState] = useState<'loading' | 'ready' | 'error'>('loading')
  const [retryKey, setRetryKey] = useState(0)

  useEffect(() => {
    setUserIdInput(userId)
  }, [userId])

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoadState('loading')
    adminApi
      .listOrders(
        { page, pageSize: PAGE_SIZE, status: status || undefined, userId: userId || undefined },
        accessToken,
        controller.signal,
      )
      .then((result) => {
        setOrders(result)
        setLoadState('ready')
      })
      .catch((error) => {
        if (!isAbortError(error)) {
          setOrders(null)
          setLoadState('error')
        }
      })
    return () => controller.abort()
  }, [accessToken, page, status, userId, retryKey])

  function setStatusFilter(nextStatus: string) {
    const next = new URLSearchParams(searchParams)
    next.set('page', '1')
    if (nextStatus) next.set('status', nextStatus)
    else next.delete('status')
    setSearchParams(next)
  }

  function applyUserIdFilter(event: React.FormEvent) {
    event.preventDefault()
    const next = new URLSearchParams(searchParams)
    next.set('page', '1')
    const trimmed = userIdInput.trim()
    if (trimmed) next.set('userId', trimmed)
    else next.delete('userId')
    setSearchParams(next)
  }

  function setPage(nextPage: number) {
    const next = new URLSearchParams(searchParams)
    next.set('page', String(nextPage))
    setSearchParams(next)
  }

function getStatusVariant(status: string): 'success' | 'warning' | 'danger' | 'neutral' | 'info' {
  switch (status) {
    case 'Delivered':
      return 'success'
    case 'Cancelled':
      return 'danger'
    case 'Shipped':
      return 'warning'
    case 'Confirmed':
    case 'Processing':
      return 'info'
    default:
      return 'neutral'
  }
}

  const totalPages = orders ? Math.ceil(orders.totalCount / PAGE_SIZE) : 1

  return (
    <main className="admin-page admin-orders" role="main" aria-label="Quản lý đơn hàng">
      <header className="admin-page-header">
        <h1>Quản lý đơn hàng</h1>
        <p className="admin-page-sub">Xem toàn bộ đơn hàng của hệ thống và cập nhật trạng thái.</p>
      </header>

      <AdminCard
        toolbar={
          <div className="admin-toolbar-wrap flex flex-col md:flex-row md:items-center md:justify-between gap-4 w-full">
            <div className="admin-toolbar-left flex flex-wrap items-center gap-3">
              <div className="admin-field" style={{ margin: 0 }}>
                <label htmlFor="admin-order-status" className="sr-only">Lọc trạng thái</label>
                <select
                  id="admin-order-status"
                  className="admin-select-filter"
                  value={status}
                  onChange={(event) => setStatusFilter(event.target.value)}
                  aria-label="Lọc trạng thái"
                >
                  <option value="">Tất cả trạng thái</option>
                  {ORDER_STATUSES.map((value) => (
                    <option key={value} value={value}>{formatStatus(value)}</option>
                  ))}
                </select>
              </div>

              <form className="admin-field flex items-center gap-2" onSubmit={applyUserIdFilter} style={{ margin: 0 }}>
                <label htmlFor="admin-order-user" className="sr-only">Lọc theo mã khách (userId)</label>
                <input
                  id="admin-order-user"
                  type="text"
                  className="admin-input-search"
                  value={userIdInput}
                  placeholder="🔍 Lọc theo userId..."
                  onChange={(event) => setUserIdInput(event.target.value)}
                />
                <button type="submit" className="admin-btn-action admin-btn-action-primary">Lọc</button>
              </form>
            </div>

            {orders && (
              <div className="admin-toolbar-right flex items-center gap-2">
                <AdminBadge variant="neutral">
                  Tổng: <strong style={{ marginLeft: 4 }}>{orders.totalCount}</strong> đơn hàng
                </AdminBadge>
              </div>
            )}
          </div>
        }
        footer={
          orders ? (
            <AdminPagination
              currentPage={page}
              totalPages={totalPages}
              totalCount={orders.totalCount}
              itemName="đơn hàng"
              onPageChange={setPage}
            />
          ) : undefined
        }
      >
        {loadState === 'error' && (
          <section className="admin-alert p-6" role="alert">
            <p className="text-rose-600 font-medium">Không thể tải danh sách đơn hàng. Vui lòng thử lại.</p>
            <button type="button" className="btn btn-sm btn-secondary mt-2" onClick={() => setRetryKey((prev) => prev + 1)}>Thử lại</button>
          </section>
        )}

        {loadState === 'loading' && (
          <p className="admin-loading py-8 text-center text-slate-500" aria-busy="true">Đang tải đơn hàng...</p>
        )}

        {loadState === 'ready' && orders && orders.data.length === 0 && (
          <AdminEmptyState
            icon="🧾"
            message="Không có đơn hàng nào khớp bộ lọc."
            description="Thử chọn trạng thái khác hoặc xóa điều kiện lọc"
          />
        )}

        {loadState === 'ready' && orders && orders.data.length > 0 && (
          <table className="admin-table admin-ds-table" aria-label="Danh sách đơn hàng" style={{ width: '100%' }}>
            <thead>
              <tr>
                <th scope="col" className="col-text text-left">Mã đơn</th>
                <th scope="col" className="col-text text-left">Ngày tạo</th>
                <th scope="col" className="col-text text-center">Hình thức</th>
                <th scope="col" className="col-numeric text-right">Số món</th>
                <th scope="col" className="col-numeric text-right">Tổng tiền</th>
                <th scope="col" className="col-status text-center">Trạng thái</th>
                <th scope="col" className="col-actions text-right"><span className="sr-only">Hành động</span></th>
              </tr>
            </thead>
            <tbody>
              {orders.data.map((order) => (
                <tr key={order.id}>
                  <td className="col-text text-left align-middle">
                    <code className="admin-code-badge" title={order.id}>{shortId(order.id)}</code>
                  </td>
                  <td className="col-text text-left align-middle text-slate-600">{formatDate(order.createdAtUtc)}</td>
                  <td className="col-text text-center align-middle">
                    <span className="text-xs font-medium text-slate-700 bg-slate-100 px-2 py-0.5 rounded">
                      {order.fulfillmentType}
                    </span>
                  </td>
                  <td className="col-numeric text-right align-middle font-medium text-slate-700">{order.itemCount}</td>
                  <td className="col-numeric text-right align-middle font-semibold text-slate-900">{formatPrice(order.totalAmount)}</td>
                  <td className="col-status text-center align-middle">
                    <AdminBadge
                      variant={getStatusVariant(order.status)}
                      dot
                      className={`admin-status admin-status--${order.status.toLowerCase()}`}
                      role="status"
                    >
                      {formatStatus(order.status)}
                      <span className="sr-only">{order.status}</span>
                    </AdminBadge>
                  </td>
                  <td className="col-actions text-right align-middle">
                    <Link
                      to={`/admin/orders/${encodeURIComponent(order.id)}`}
                      className="admin-link admin-btn-action"
                      aria-label={`Xem chi tiết đơn ${order.id}`}
                    >
                      Chi tiết
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </AdminCard>
    </main>
  )
}
