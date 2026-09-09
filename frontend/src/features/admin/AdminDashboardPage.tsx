import { useEffect, useState, useCallback, useRef } from 'react'
import { Link } from 'react-router-dom'
import { adminApi, type DashboardSummaryDto } from '../../api/adminApi'
import { useAuth } from '../auth/AuthContext'
import './AdminDashboardPage.css'

function formatVnd(amount: number) {
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount)
}

function formatDate(dateStr: string) {
  try {
    const d = new Date(dateStr)
    return d.toLocaleString('vi-VN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    })
  } catch {
    return dateStr
  }
}

function getStatusBadgeClass(status: string) {
  switch (status.toLowerCase()) {
    case 'completed':
      return 'admin-dashboard__badge--completed'
    case 'pending':
      return 'admin-dashboard__badge--pending'
    case 'cancelled':
      return 'admin-dashboard__badge--cancelled'
    default:
      return 'admin-dashboard__badge--default'
  }
}

type DashboardState =
  | { kind: 'loading' }
  | { kind: 'ready'; data: DashboardSummaryDto }
  | { kind: 'error'; message: string }

export function AdminDashboardPage() {
  const { accessToken } = useAuth()
  const [state, setState] = useState<DashboardState>({ kind: 'loading' })
  const abortControllerRef = useRef<AbortSignal | null>(null)

  const fetchSummary = useCallback(async () => {
    if (!accessToken) return

    setState({ kind: 'loading' })
    const controller = new AbortController()
    abortControllerRef.current = controller.signal

    try {
      const summary = await adminApi.getDashboardSummary(accessToken, controller.signal)
      setState({ kind: 'ready', data: summary })
    } catch (err: any) {
      if (err instanceof Error && err.name === 'AbortError') return
      setState({ kind: 'error', message: err.message || 'Không thể tải dữ liệu tổng quan.' })
    }
  }, [accessToken])

  useEffect(() => {
    fetchSummary()
    return () => {
      // Abort pending request on unmount
    }
  }, [fetchSummary])

  const isLoading = state.kind === 'loading'

  return (
    <div className="admin-dashboard" role="region" aria-label="Tổng quan" aria-busy={isLoading}>
      <div className="admin-dashboard__header">
        <h1 className="admin-dashboard__title">Tổng quan hệ thống</h1>
      </div>

      {state.kind === 'loading' && (
        <div className="admin-dashboard__loading">
          <span>Đang tải dữ liệu tổng quan...</span>
        </div>
      )}

      {state.kind === 'error' && (
        <div className="admin-dashboard__error" role="alert">
          <span>{state.message}</span>
          <button type="button" className="admin-dashboard__retry-btn" onClick={fetchSummary}>
            Thử lại
          </button>
        </div>
      )}

      {state.kind === 'ready' && (
        <>
          <div className="admin-dashboard__metrics">
            <div className="admin-dashboard__card">
              <div className="admin-dashboard__card-header">
                <span>Tổng số đơn hàng</span>
                <span aria-hidden="true">🧾</span>
              </div>
              <div className="admin-dashboard__card-value">{state.data.totalOrders}</div>
            </div>

            <div className="admin-dashboard__card">
              <div className="admin-dashboard__card-header">
                <span>Đơn cần xử lý</span>
                <span aria-hidden="true">⏳</span>
              </div>
              <div className="admin-dashboard__card-value">{state.data.pendingOrders}</div>
            </div>

            <div className="admin-dashboard__card admin-dashboard__card--revenue">
              <div className="admin-dashboard__card-header">
                <span>Tổng doanh thu đơn hoàn tất</span>
                <span aria-hidden="true">💰</span>
              </div>
              <div className="admin-dashboard__card-value">{formatVnd(state.data.completedRevenue)}</div>
            </div>

            <div className="admin-dashboard__card admin-dashboard__card--warning">
              <div className="admin-dashboard__card-header">
                <span>Hàng tồn thấp</span>
                <span aria-hidden="true">⚠️</span>
              </div>
              <div className="admin-dashboard__card-value">{state.data.lowStockItems}</div>
            </div>
          </div>

          <section className="admin-dashboard__section">
            <h2 className="admin-dashboard__section-title">Đơn hàng gần đây</h2>
            {state.data.recentOrders.length === 0 ? (
              <div className="admin-dashboard__empty">
                <p>Chưa có đơn hàng nào gần đây.</p>
              </div>
            ) : (
              <table className="admin-dashboard__table">
                <thead>
                  <tr>
                    <th>Mã đơn</th>
                    <th>Thời gian</th>
                    <th>Số món</th>
                    <th>Hình thức</th>
                    <th>Tổng tiền</th>
                    <th>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {state.data.recentOrders.map((order) => (
                    <tr key={order.id}>
                      <td>
                        <Link to={`/admin/orders/${order.id}`} className="admin-dashboard__order-link">
                          {order.id}
                        </Link>
                      </td>
                      <td>{formatDate(order.createdAtUtc)}</td>
                      <td>{order.itemCount}</td>
                      <td>{order.fulfillmentType === 'Pickup' ? 'Nhận tại kho' : 'Giao hàng'}</td>
                      <td>{formatVnd(order.totalAmount)}</td>
                      <td>
                        <span className={`admin-dashboard__badge ${getStatusBadgeClass(order.status)}`}>
                          {order.status}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
        </>
      )}
    </div>
  )
}
