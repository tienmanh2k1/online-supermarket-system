import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { orderApi, type OrderDetailDto, type OrderItemDto } from '../../api/orderApi'
import { ApiError } from '../../api/httpClient'
import './OrderHistoryPage.css'

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === 'AbortError'
}

function formatPrice(value: number) {
  return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(value)
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

const STATUS_LABELS: Record<string, string> = {
  Pending: 'Chờ xác nhận',
  Confirmed: 'Đã xác nhận',
  Preparing: 'Đang chuẩn bị',
  Ready: 'Sẵn sàng nhận hàng',
  Shipped: 'Đang giao hàng',
  Delivered: 'Đã giao hàng',
  Completed: 'Hoàn tất',
  Cancelled: 'Đã hủy',
  Failed: 'Thất bại',
}

function formatStatus(status: string) {
  return STATUS_LABELS[status] ?? status
}

function OrderDetailSkeleton() {
  return (
    <section className="orders-page order-detail-page">
      <p>Đang tải chi tiết đơn hàng...</p>
    </section>
  )
}

function OrderDetailAuthRequired() {
  return (
    <section className="orders-page order-detail-page">
      <h1>Đăng nhập để xem đơn hàng</h1>
      <p>Vui lòng đăng nhập để xem chi tiết đơn hàng.</p>
    </section>
  )
}

function OrderNotFound() {
  return (
    <section className="orders-page order-detail-page">
      <h1>Không tìm thấy đơn hàng</h1>
      <p>Đơn hàng không tồn tại hoặc bạn không có quyền xem.</p>
      <Link to="/orders/history">Quay lại lịch sử đơn hàng</Link>
    </section>
  )
}

function OrderDetailError({ onRetry }: { onRetry: () => void }) {
  return (
    <section className="orders-page order-detail-page">
      <h1>Không thể tải chi tiết đơn hàng</h1>
      <p>Vui lòng thử lại sau ít phút.</p>
      <button type="button" onClick={onRetry}>Thử lại</button>
    </section>
  )
}

function OrderItemRow({ item }: { item: OrderItemDto }) {
  return (
    <div className="order-item-row">
      <span>{item.productName}</span>
      <span>{item.sku}</span>
      <span>{formatPrice(item.unitPrice)} x {item.quantity}</span>
      <span>{formatPrice(item.lineTotal)}</span>
      {(item.canReview || item.reviewId) && (
        <div className="order-item-actions">
          {item.canReview ? (
            <Link
              to={`/product/${item.productId}?reviewOrderItemId=${item.orderItemId}#reviews`}
              className="order-item-review-link"
            >
              Viết đánh giá {item.productName}
            </Link>
          ) : item.reviewId ? (
            <Link
              to={`/product/${item.productId}?reviewId=${item.reviewId}#reviews`}
              className="order-item-review-link"
            >
              Sửa đánh giá {item.productName}
            </Link>
          ) : null}
        </div>
      )}
    </div>
  )
}

function StatusTimelineItem({ entry }: { entry: { fromStatus: string; toStatus: string; note: string | null; createdAtUtc: string } }) {
  return (
    <div className="status-timeline-item">
      <span>{entry.fromStatus} → {entry.toStatus}</span>
      {entry.note && <span>{entry.note}</span>}
      <span>{formatDate(entry.createdAtUtc)}</span>
    </div>
  )
}

function OrderDetailContent({ order }: { order: OrderDetailDto }) {
  return (
    <section className="orders-page order-detail-page">
      <nav aria-label="Breadcrumb">
        <Link to="/orders/history">Lịch sử đơn hàng</Link>
      </nav>
      <h1>Chi tiết đơn hàng {order.id}</h1>
      <div className="order-detail-layout">
        <div className="order-detail-main">
          <section aria-label="Thông tin giao hàng">
            <h2>{order.fulfillmentType === 'Pickup' ? 'Thông tin nhận hàng' : 'Thông tin giao hàng'}</h2>
            {order.fulfillmentType === 'Pickup' ? (
              <>
                <p><strong>Hình thức:</strong> Nhận tại chi nhánh</p>
                {order.recipientName && order.recipientName !== 'N/A' && <p>Người nhận: {order.recipientName}</p>}
                {order.recipientPhone && order.recipientPhone !== 'N/A' && <p>Số điện thoại: {order.recipientPhone}</p>}
                <p>{order.deliveryAddressSnapshot === 'Pickup at branch' ? 'Nhận hàng trực tiếp tại quầy chi nhánh' : order.deliveryAddressSnapshot}</p>
              </>
            ) : (
              <>
                <p>{order.recipientName}</p>
                <p>{order.recipientPhone}</p>
                <p>{order.deliveryAddressSnapshot}</p>
              </>
            )}
          </section>
          <section aria-label="Sản phẩm trong đơn">
            <h2>Sản phẩm</h2>
            {order.items.map((item) => (
              <OrderItemRow key={item.productId} item={item} />
            ))}
          </section>
          <section aria-label="Lịch sử trạng thái">
            <h2>Lịch sử trạng thái</h2>
            {order.statusHistory.map((entry, index) => (
              <StatusTimelineItem key={`${entry.toStatus}-${entry.createdAtUtc}-${index}`} entry={entry} />
            ))}
          </section>
        </div>
        <div className="order-detail-sidebar">
          <section aria-label="Tóm tắt đơn hàng">
            <h2>Tóm tắt</h2>
            <p>Tạm tính: {formatPrice(order.subtotal)}</p>
            {order.discountAmount > 0 && <p>Giảm giá: -{formatPrice(order.discountAmount)}</p>}
            <p>Phí giao hàng: {formatPrice(order.shippingFee)}</p>
            <p><strong>Tổng cộng: {formatPrice(order.totalAmount)}</strong></p>
          </section>
          <section aria-label="Thanh toán">
            <h2>Thanh toán</h2>
            {order.payment ? (
              <>
                <p>{order.payment.method}</p>
                <p>{order.payment.status}</p>
                <p>{formatPrice(order.payment.amount)}</p>
              </>
            ) : (
              <p>Chưa ghi nhận</p>
            )}
          </section>
          <section aria-label="Trạng thái">
            <h2>Trạng thái</h2>
            <span className={`order-status order-status--${order.status.toLowerCase()}`}>
              {formatStatus(order.status)}
              <span className="sr-only">{order.status}</span>
            </span>
          </section>
        </div>
      </div>
    </section>
  )
}

export function OrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { isAuthenticated, isLoading: authLoading, accessToken } = useAuth()
  const [order, setOrder] = useState<OrderDetailDto | null>(null)
  const [loadState, setLoadState] = useState<'loading' | 'ready' | 'not-found' | 'error'>('loading')
  const [retryKey, setRetryKey] = useState(0)

  useEffect(() => {
    if (!isAuthenticated || !accessToken || !id) return
    const controller = new AbortController()
    setLoadState('loading')
    orderApi.getOrderById(id, accessToken, controller.signal)
      .then((result) => {
        setOrder(result)
        setLoadState('ready')
      })
      .catch((error) => {
        if (isAbortError(error)) return
        setLoadState(error instanceof ApiError && error.status === 404 ? 'not-found' : 'error')
      })
    return () => controller.abort()
  }, [isAuthenticated, accessToken, id, retryKey])

  if (authLoading) return <OrderDetailSkeleton />
  if (!isAuthenticated) return <OrderDetailAuthRequired />
  if (loadState === 'loading') return <OrderDetailSkeleton />
  if (loadState === 'not-found') return <OrderNotFound />
  if (loadState === 'error') return <OrderDetailError onRetry={() => setRetryKey((key) => key + 1)} />
  if (!order) return <OrderDetailError onRetry={() => setRetryKey((key) => key + 1)} />

  return <OrderDetailContent order={order} />
}
