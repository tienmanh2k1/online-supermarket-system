import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { adminApi } from '../../api/adminApi'
import type { OrderDetailDto } from '../../api/orderApi'
import { ApiError } from '../../api/httpClient'
import { formatStatus, validTransitions } from './orderStatus'
import './AdminOrdersPage.css'
import './AdminDesignSystem.css'

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === 'AbortError'
}

function formatPrice(value: number) {
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value)
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

function StatusUpdatePanel({
  order,
  onUpdated,
}: {
  order: OrderDetailDto
  onUpdated: (updated: OrderDetailDto) => void
}) {
  const { accessToken } = useAuth()
  const transitions = validTransitions(order.status)
  const [selectedStatus, setSelectedStatus] = useState('')
  const [note, setNote] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Reset the form whenever the order (and thus its allowed transitions) changes.
  useEffect(() => {
    setSelectedStatus('')
    setNote('')
    setError(null)
  }, [order.id, order.status])

  if (transitions.length === 0) {
    return (
      <section className="admin-status-panel" aria-label="Cập nhật trạng thái">
        <h2>Cập nhật trạng thái</h2>
        <p className="admin-status-final">Đơn đang ở trạng thái cuối, không thể chuyển tiếp.</p>
      </section>
    )
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (!selectedStatus || !accessToken) return
    setSubmitting(true)
    setError(null)
    try {
      const updated = await adminApi.updateOrderStatus(
        order.id,
        { status: selectedStatus, note: note.trim() || undefined },
        accessToken,
      )
      onUpdated(updated)
    } catch (err) {
      if (err instanceof ApiError && err.data?.message) {
        setError(err.data.message)
      } else {
        setError('Không thể cập nhật trạng thái. Vui lòng thử lại.')
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className="admin-status-panel" aria-label="Cập nhật trạng thái">
      <h2>Cập nhật trạng thái</h2>
      <form onSubmit={submit}>
        <div className="admin-field">
          <label htmlFor="admin-next-status">Trạng thái mới</label>
          <select
            id="admin-next-status"
            value={selectedStatus}
            onChange={(event) => setSelectedStatus(event.target.value)}
          >
            <option value="">-- Chọn trạng thái --</option>
            {transitions.map((value) => (
              <option key={value} value={value}>{formatStatus(value)}</option>
            ))}
          </select>
        </div>
        <div className="admin-field">
          <label htmlFor="admin-status-note">Ghi chú (tùy chọn)</label>
          <input
            id="admin-status-note"
            type="text"
            value={note}
            onChange={(event) => setNote(event.target.value)}
            placeholder="Lý do / ghi chú..."
          />
        </div>
        {selectedStatus === 'Cancelled' && (
          <p className="admin-warning" role="alert">
            Hủy đơn sẽ tự động hoàn trả tồn kho đã giữ. Hành động này không thể hoàn tác.
          </p>
        )}
        {error && <p className="admin-error" role="alert">{error}</p>}
        <button type="submit" className="btn-primary" disabled={!selectedStatus || submitting}>
          {submitting ? 'Đang cập nhật...' : 'Cập nhật trạng thái'}
        </button>
      </form>
    </section>
  )
}

function AdminOrderContent({
  order,
  onUpdated,
}: {
  order: OrderDetailDto
  onUpdated: (updated: OrderDetailDto) => void
}) {
  return (
    <main className="admin-page admin-order-detail" role="main" aria-label="Chi tiết đơn hàng">
      <nav aria-label="Breadcrumb" className="admin-breadcrumb">
        <Link to="/admin/orders">← Danh sách đơn hàng</Link>
      </nav>
      <header className="admin-page-header">
        <h1>Chi tiết đơn hàng</h1>
        <p className="admin-page-sub"><code>{order.id}</code></p>
        <span className={`admin-status admin-status--${order.status.toLowerCase()}`} role="status">
          {formatStatus(order.status)}
          <span className="sr-only">{order.status}</span>
        </span>
      </header>

      <div className="admin-detail-grid">
        <div className="admin-detail-main">
          <section aria-label="Thông tin giao hàng" className="admin-card">
            <h2>{order.fulfillmentType === 'Pickup' ? 'Thông tin nhận hàng' : 'Thông tin giao hàng'}</h2>
            <p><strong>Hình thức:</strong> {order.fulfillmentType === 'Pickup' ? 'Nhận tại chi nhánh' : 'Giao hàng tận nơi'}</p>
            <p><strong>Người nhận:</strong> {order.recipientName && order.recipientName !== 'N/A' ? order.recipientName : (order.fulfillmentType === 'Pickup' ? 'Khách nhận tại quầy' : '—')}</p>
            <p><strong>Số điện thoại:</strong> {order.recipientPhone && order.recipientPhone !== 'N/A' ? order.recipientPhone : '—'}</p>
            <p><strong>Địa chỉ:</strong> {order.deliveryAddressSnapshot === 'Pickup at branch' ? 'Nhận hàng trực tiếp tại quầy chi nhánh' : (order.deliveryAddressSnapshot || '—')}</p>
          </section>

          <section aria-label="Sản phẩm trong đơn" className="admin-card bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden" style={{ padding: 0 }}>
            <div className="p-4 border-b border-slate-100 flex items-center justify-between">
              <h2 className="text-base font-semibold text-slate-800" style={{ margin: 0 }}>Sản phẩm trong đơn</h2>
              <span className="text-xs font-semibold text-slate-500">{order.items.length} mặt hàng</span>
            </div>
            <div className="admin-table-wrap overflow-x-auto">
              <table className="admin-table admin-ds-table" aria-label="Sản phẩm trong đơn" style={{ width: '100%' }}>
                <thead>
                  <tr>
                    <th scope="col" className="col-text text-left">Sản phẩm</th>
                    <th scope="col" className="col-text text-left">SKU</th>
                    <th scope="col" className="col-numeric text-right">Đơn giá</th>
                    <th scope="col" className="col-numeric text-right">SL</th>
                    <th scope="col" className="col-numeric text-right">Thành tiền</th>
                  </tr>
                </thead>
                <tbody>
                  {order.items.map((item) => (
                    <tr key={item.productId}>
                      <td className="col-text text-left align-middle font-medium text-slate-800">{item.productName}</td>
                      <td className="col-text text-left align-middle"><code className="admin-code-badge">{item.sku}</code></td>
                      <td className="col-numeric text-right align-middle text-slate-700">{formatPrice(item.unitPrice)}</td>
                      <td className="col-numeric text-right align-middle font-medium text-slate-700">{item.quantity}</td>
                      <td className="col-numeric text-right align-middle font-semibold text-slate-900">{formatPrice(item.lineTotal)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <section aria-label="Lịch sử trạng thái" className="admin-card">
            <h2>Lịch sử trạng thái</h2>
            <ol className="admin-timeline">
              {order.statusHistory.map((entry, index) => (
                <li key={`${entry.toStatus}-${entry.createdAtUtc}-${index}`}>
                  <span className="admin-timeline-status">
                    {formatStatus(entry.fromStatus)} → {formatStatus(entry.toStatus)}
                  </span>
                  {entry.note && <span className="admin-timeline-note">{entry.note}</span>}
                  <span className="admin-timeline-date">{formatDate(entry.createdAtUtc)}</span>
                </li>
              ))}
            </ol>
          </section>
        </div>

        <aside className="admin-detail-side">
          <section aria-label="Tóm tắt đơn hàng" className="admin-card">
            <h2>Tóm tắt</h2>
            <p>Tạm tính: {formatPrice(order.subtotal)}</p>
            {order.discountAmount > 0 && <p>Giảm giá: -{formatPrice(order.discountAmount)}</p>}
            <p>Phí giao hàng: {formatPrice(order.shippingFee)}</p>
            <p className="admin-total"><strong>Tổng cộng: {formatPrice(order.totalAmount)}</strong></p>
          </section>

          <section aria-label="Thanh toán" className="admin-card">
            <h2>Thanh toán</h2>
            {order.payment ? (
              <>
                <p>Phương thức: {order.payment.method}</p>
                <p>Trạng thái: {order.payment.status}</p>
                <p>Số tiền: {formatPrice(order.payment.amount)}</p>
                {order.payment.isMock && <p>Giả lập — không thu tiền</p>}
              </>
            ) : (
              <p>Chưa ghi nhận thanh toán.</p>
            )}
          </section>

          <StatusUpdatePanel order={order} onUpdated={onUpdated} />
        </aside>
      </div>
    </main>
  )
}

export function AdminOrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { accessToken } = useAuth()
  const [order, setOrder] = useState<OrderDetailDto | null>(null)
  const [loadState, setLoadState] = useState<'loading' | 'ready' | 'not-found' | 'error'>('loading')
  const [retryKey, setRetryKey] = useState(0)

  useEffect(() => {
    if (!accessToken || !id) return
    const controller = new AbortController()
    setLoadState('loading')
    adminApi
      .getOrderById(id, accessToken, controller.signal)
      .then((result) => {
        setOrder(result)
        setLoadState('ready')
      })
      .catch((error) => {
        if (isAbortError(error)) return
        setLoadState(error instanceof ApiError && error.status === 404 ? 'not-found' : 'error')
      })
    return () => controller.abort()
  }, [accessToken, id, retryKey])

  if (loadState === 'loading') {
    return (
      <section className="admin-page admin-order-detail" aria-busy="true">
        <p>Đang tải chi tiết đơn hàng...</p>
      </section>
    )
  }

  if (loadState === 'not-found') {
    return (
      <section className="admin-page admin-order-detail">
        <h1>Không tìm thấy đơn hàng</h1>
        <p>Đơn hàng không tồn tại.</p>
        <Link to="/admin/orders">Quay lại danh sách đơn hàng</Link>
      </section>
    )
  }

  if (loadState === 'error' || !order) {
    return (
      <section className="admin-page admin-order-detail">
        <h1>Không thể tải chi tiết đơn hàng</h1>
        <p>Vui lòng thử lại sau ít phút.</p>
        <button type="button" onClick={() => setRetryKey((key) => key + 1)}>Thử lại</button>
      </section>
    )
  }

  return <AdminOrderContent order={order} onUpdated={setOrder} />
}
