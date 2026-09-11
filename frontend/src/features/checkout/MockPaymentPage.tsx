import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { mockPaymentApi, type MockOutcome, type MockPaymentDto } from '../../api/mockPaymentApi'
import { useAuth } from '../auth/AuthContext'
import { formatPrice } from '../products/ProductCard'
import './CheckoutPage.css'

export function MockPaymentPage() {
  const { paymentId } = useParams()
  const { accessToken, isAuthenticated } = useAuth()
  const [payment, setPayment] = useState<MockPaymentDto | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const load = async () => {
    if (!paymentId || !accessToken) return
    try {
      setPayment(await mockPaymentApi.get(paymentId, accessToken))
      setError(null)
    } catch {
      setError('Không thể tải trạng thái thanh toán.')
    }
  }
  useEffect(() => { void load() }, [paymentId, accessToken])
  const complete = async (outcome: MockOutcome) => {
    if (!paymentId || !accessToken) return
    setBusy(true)
    try {
      setPayment(await mockPaymentApi.complete(paymentId, outcome, accessToken))
      setError(null)
    } catch {
      await load()
      setError('Không thể cập nhật kết quả. Vui lòng kiểm tra lại trạng thái trước khi thử lại.')
    } finally {
      setBusy(false)
    }
  }
  if (!isAuthenticated) return <section className="checkout-page">Vui lòng đăng nhập để tiếp tục thanh toán.</section>
  if (!payment) return <section className="checkout-page">{error ?? 'Đang tải giao dịch…'}{error && <button type="button" onClick={() => void load()}>Thử lại</button>}</section>
  const terminal = payment.paymentStatus !== 'Pending' || payment.orderStatus === 'Cancelled'
  const result = payment.outcome === 'Cancelled' ? 'Thanh toán đã được hủy.' : payment.paymentStatus === 'Completed' ? 'Đã thanh toán giả lập.' : 'Thanh toán đã thất bại.'
  return <section className="checkout-page"><div className="checkout-success-card"><h1>Thanh toán giả lập — không thu tiền</h1><p className="checkout-success-message">{payment.method} · {formatPrice(payment.amount)}</p>{terminal ? <><p>{result}</p><Link className="checkout-btn checkout-btn--primary" to={`/orders/history/${payment.orderId}`}>Xem đơn hàng</Link></> : <div className="checkout-success-actions"><button disabled={busy} className="checkout-btn checkout-btn--primary" onClick={() => void complete('Success')}>Thanh toán thành công</button><button disabled={busy} className="checkout-btn checkout-btn--secondary" onClick={() => void complete('Failed')}>Thanh toán thất bại</button><button disabled={busy} className="checkout-btn checkout-btn--secondary" onClick={() => void complete('Cancelled')}>Hủy thanh toán</button></div>}{error && <p role="alert" className="checkout-alert">{error}</p>}</div></section>
}
