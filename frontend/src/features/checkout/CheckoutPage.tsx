import React, { useState, useEffect } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { useCart } from '../cart/CartContext'
import { AuthModal } from '../auth/AuthModal'
import { formatPrice } from '../products/ProductCard'
import {
  checkoutApi,
  type FulfillmentType,
  type PaymentMethod,
  type CheckoutRequest,
  type PaymentInitDto,
  type PaymentOptionsDto,
} from '../../api/checkoutApi'
import { getAddressesApi, type AddressDto } from '../../api/addressApi'
import { ApiError } from '../../api/httpClient'
import type { CartDto } from '../../api/cartApi'
import './CheckoutPage.css'

const DELIVERY_SHIPPING_FEE = 15000

function isAbortError(error: unknown) {
  return error instanceof Error && error.name === 'AbortError'
}

export function toCheckoutError(error: unknown) {
  if (
    error instanceof ApiError &&
    error.status === 400 &&
    error.data?.message === 'CART_EMPTY'
  ) {
    return 'Giỏ hàng không còn sản phẩm.'
  }
  if (error instanceof ApiError && error.status === 400 && typeof error.data?.message === 'string') {
    const couponMessages: Record<string, string> = {
      INVALID_COUPON: 'Mã giảm giá không hợp lệ. Vui lòng gỡ mã và thử lại.',
      COUPON_INACTIVE: 'Mã giảm giá đã ngừng áp dụng. Vui lòng gỡ mã.',
      COUPON_EXHAUSTED: 'Mã giảm giá đã hết lượt sử dụng. Vui lòng gỡ mã.',
      MIN_ORDER_NOT_MET: 'Đơn hàng chưa đạt giá trị tối thiểu để áp mã. Vui lòng gỡ mã.',
    }
    const mapped = couponMessages[error.data.message]
    if (mapped) return mapped
  }
  if (
    error instanceof ApiError &&
    error.status === 409 &&
    error.data?.message === 'INSUFFICIENT_STOCK'
  ) {
    return 'Một số sản phẩm không còn đủ tồn kho. Vui lòng quay lại giỏ hàng để cập nhật số lượng.'
  }
  if (error instanceof ApiError && error.status === 401) {
    return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'
  }
  if (error instanceof ApiError && error.status === 404) {
    return 'Không tìm thấy đơn hàng để thanh toán.'
  }
  return 'Không thể đặt hàng. Vui lòng thử lại.'
}

function formatAddress(address: AddressDto) {
  return [address.street, address.ward, address.district, address.city]
    .filter(Boolean)
    .join(', ')
}

function CheckoutSkeleton() {
  return (
    <section className="checkout-page" aria-busy="true" aria-label="Đang tải thông tin thanh toán">
      <div className="checkout-page__skeleton shimmer" />
    </section>
  )
}

function CheckoutAuthRequired({
  openLogin,
  loginOpen,
  setLoginOpen,
}: {
  openLogin: () => void
  loginOpen: boolean
  setLoginOpen: (open: boolean) => void
}) {
  return (
    <section className="checkout-page checkout-page--auth-required">
      <h1>Đăng nhập để thanh toán</h1>
      <p>Vui lòng đăng nhập để tiếp tục thanh toán và bảo vệ đơn hàng của bạn.</p>
      <button type="button" className="checkout-btn checkout-btn--primary" onClick={openLogin}>
        Đăng nhập
      </button>
      <AuthModal
        isOpen={loginOpen}
        initialMode="login"
        onClose={() => setLoginOpen(false)}
      />
    </section>
  )
}

function CheckoutLoadError({ onRetry }: { onRetry: () => void }) {
  return (
    <section className="checkout-page">
      <div className="checkout-page__error" role="alert">
        <h2>Không thể tải thông tin thanh toán</h2>
        <p>Đã xảy ra sự cố khi tải giỏ hàng. Vui lòng thử lại.</p>
        <button type="button" className="checkout-btn checkout-btn--primary" onClick={onRetry}>
          Thử lại
        </button>
      </div>
    </section>
  )
}

function EmptyCheckout() {
  return (
    <section className="checkout-page">
      <div className="checkout-page__empty">
        <h2>Giỏ hàng đang trống</h2>
        <p>Không có sản phẩm nào trong giỏ hàng để tiến hành thanh toán.</p>
        <Link to="/shopping/cart" className="checkout-btn checkout-btn--primary">
          Quay lại giỏ hàng
        </Link>
      </div>
    </section>
  )
}

function CheckoutOrderSummary({
  cart,
  shippingFee,
  discount,
}: {
  cart: CartDto
  shippingFee: number
  discount: number
}) {
  const totalAmount = Math.max(0, cart.subtotal - discount) + shippingFee

  return (
    <aside className="checkout-summary" aria-label="Tóm tắt đơn hàng">
      <h2 className="checkout-summary__title">Tóm tắt đơn hàng</h2>
      <div className="checkout-summary__items">
        {cart.items.map((item) => (
          <div key={item.id} className="checkout-summary__item">
            <div>
              <div className="checkout-summary__item-name">{item.productName}</div>
              <div className="checkout-summary__item-qty">Số lượng: {item.quantity}</div>
            </div>
            <div className="checkout-summary__item-price">{formatPrice(item.lineTotal)}</div>
          </div>
        ))}
      </div>

      <div className="checkout-summary__rows">
        <div className="checkout-summary__row">
          <span>Tạm tính</span>
          <strong>{formatPrice(cart.subtotal)}</strong>
        </div>
        {discount > 0 && (
          <div className="checkout-summary__row checkout-summary__row--discount">
            <span>Giảm giá</span>
            <strong>-{formatPrice(discount)}</strong>
          </div>
        )}
        <div className="checkout-summary__row">
          <span>Phí giao hàng</span>
          <strong>{formatPrice(shippingFee)}</strong>
        </div>
        <div className="checkout-summary__row checkout-summary__row--total">
          <span>Tổng thanh toán</span>
          <strong className="checkout-summary__price--highlight">{formatPrice(totalAmount)}</strong>
        </div>
      </div>
    </aside>
  )
}

export function CheckoutPage() {
  const { isAuthenticated, isLoading: authLoading, accessToken } = useAuth()
  const { status, cart, reloadCart } = useCart()
  const navigate = useNavigate()

  const [loginOpen, setLoginOpen] = useState(false)
  const [fulfillmentType, setFulfillmentType] = useState<FulfillmentType>('Pickup')
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>('COD')
  const [addresses, setAddresses] = useState<AddressDto[]>([])
  const [selectedAddressId, setSelectedAddressId] = useState<string>('')
  const [recipientName, setRecipientName] = useState('')
  const [recipientPhone, setRecipientPhone] = useState('')
  const [deliveryAddress, setDeliveryAddress] = useState('')
  const [addressLoadError, setAddressLoadError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [createdOrderId, setCreatedOrderId] = useState<string | null>(null)
  const [couponInput, setCouponInput] = useState('')
  const [appliedCoupon, setAppliedCoupon] = useState<{ code: string; discountAmount: number } | null>(null)
  const [couponError, setCouponError] = useState<string | null>(null)
  const [couponChecking, setCouponChecking] = useState(false)
  const [paymentOptions, setPaymentOptions] = useState<PaymentOptionsDto | null>(null)

  useEffect(() => {
    if (!isAuthenticated || !accessToken) return
    const controller = new AbortController()
    getAddressesApi(accessToken, controller.signal)
      .then((items) => {
        setAddresses(items)
        const selected = items.find((item) => item.isDefault) ?? items[0]
        if (selected) {
          setSelectedAddressId(selected.id)
          setRecipientName(selected.recipientName)
          setRecipientPhone(selected.phone)
          setDeliveryAddress(formatAddress(selected))
        }
      })
      .catch((error) => {
        if (!isAbortError(error)) {
          setAddressLoadError('Không thể tải sổ địa chỉ. Bạn vẫn có thể nhập địa chỉ thủ công.')
        }
      })
    return () => controller.abort()
  }, [isAuthenticated, accessToken])

  useEffect(() => {
    if (!isAuthenticated || !accessToken) return
    const controller = new AbortController()
    checkoutApi.getPaymentOptions(accessToken, controller.signal)
      .then((options) => {
        setPaymentOptions(options)
        if (!options.onlineEnabled) setPaymentMethod((current) => current === 'COD' ? current : 'COD')
      })
      .catch((error) => {
        if (!isAbortError(error)) {
          setPaymentOptions({ mode: 'Sandbox', onlineEnabled: false, disabledReason: 'Không thể tải tùy chọn thanh toán online.' })
        }
      })
    return () => controller.abort()
  }, [isAuthenticated, accessToken])

  const handleSavedAddressChange = (addressId: string) => {
    setSelectedAddressId(addressId)
    const found = addresses.find((a) => a.id === addressId)
    if (found) {
      setRecipientName(found.recipientName)
      setRecipientPhone(found.phone)
      setDeliveryAddress(formatAddress(found))
    }
  }

  const handleApplyCoupon = async () => {
    const code = couponInput.trim()
    if (!code || !accessToken) return
    setCouponChecking(true)
    setCouponError(null)
    try {
      const result = await checkoutApi.validateCoupon({ code }, accessToken)
      if (result.valid) {
        setAppliedCoupon({ code: code.toUpperCase(), discountAmount: result.discountAmount })
      } else {
        setAppliedCoupon(null)
        setCouponError(result.message)
      }
    } catch {
      setAppliedCoupon(null)
      setCouponError('Không thể kiểm tra mã giảm giá. Vui lòng thử lại.')
    } finally {
      setCouponChecking(false)
    }
  }

  const handleRemoveCoupon = () => {
    setAppliedCoupon(null)
    setCouponInput('')
    setCouponError(null)
  }

  function buildCheckoutRequest(): CheckoutRequest | null {
    if (fulfillmentType === 'Pickup') {
      const request: CheckoutRequest = { fulfillmentType: 'Pickup' }
      if (appliedCoupon) request.couponCode = appliedCoupon.code
      return request
    }
    if (!recipientName.trim() || !recipientPhone.trim() || !deliveryAddress.trim()) {
      return null
    }
    const request: CheckoutRequest = {
      fulfillmentType: 'Delivery',
      deliveryAddressId: selectedAddressId || null,
      recipientName: recipientName.trim(),
      recipientPhone: recipientPhone.trim(),
      deliveryAddress: deliveryAddress.trim(),
    }
    if (appliedCoupon) request.couponCode = appliedCoupon.code
    return request
  }

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!accessToken) return

    let currentOrderId = createdOrderId

    if (!currentOrderId) {
      const request = buildCheckoutRequest()
      if (!request) {
        setSubmitError('Vui lòng nhập đầy đủ thông tin giao hàng.')
        return
      }

      setIsSubmitting(true)
      setSubmitError(null)
      const controller = new AbortController()

      try {
        const order = await checkoutApi.checkout(request, accessToken, controller.signal)
        currentOrderId = order.orderId
        setCreatedOrderId(order.orderId)
      } catch (error) {
        if (!isAbortError(error)) {
          setSubmitError(toCheckoutError(error))
        }
        setIsSubmitting(false)
        return
      }
    } else {
      setIsSubmitting(true)
      setSubmitError(null)
    }

    const controller = new AbortController()
    let payment: PaymentInitDto
    try {
      payment = await checkoutApi.initiatePayment(
        { orderId: currentOrderId, method: paymentMethod },
        accessToken,
        controller.signal
      )
    } catch (error) {
      if (!isAbortError(error)) {
        setSubmitError(
          `Đơn hàng (${currentOrderId}) đã được tạo nhưng chưa thể khởi tạo thanh toán. Vui lòng chọn lại phương thức và thử lại.`
        )
      }
      setIsSubmitting(false)
      return
    }

    try {
      await reloadCart()
    } catch {
      // Best-effort cart refresh; failures should not block successful payment completion
    } finally {
      setIsSubmitting(false)
    }

    if (payment.checkoutUrl) {
      navigate(payment.checkoutUrl)
      return
    }

    navigate(
      `/shopping/checkout/success?orderId=${encodeURIComponent(
        currentOrderId
      )}&paymentStatus=${encodeURIComponent(payment.status)}`
    )
  }

  if (authLoading) return <CheckoutSkeleton />
  if (!isAuthenticated) {
    return (
      <CheckoutAuthRequired
        openLogin={() => setLoginOpen(true)}
        loginOpen={loginOpen}
        setLoginOpen={setLoginOpen}
      />
    )
  }
  if (status === 'loading' || status === 'idle') return <CheckoutSkeleton />
  if (status === 'error' || !cart) return <CheckoutLoadError onRetry={() => void reloadCart()} />
  if (cart.items.length === 0 && !createdOrderId) return <EmptyCheckout />

  const shippingFee = fulfillmentType === 'Delivery' ? DELIVERY_SHIPPING_FEE : 0

  return (
    <section className="checkout-page">
      <nav aria-label="Breadcrumb" className="checkout-breadcrumb">
        <Link to="/shopping/cart">Giỏ hàng</Link>
      </nav>
      <h1 className="checkout-page__heading">Thanh toán đơn hàng</h1>

      <div className="checkout-page__layout">
        <form
          noValidate
          className="checkout-form"
          aria-label="Thông tin thanh toán"
          onSubmit={handleSubmit}
        >
          <fieldset
            className="checkout-form__section"
            role="group"
            aria-label="Hình thức nhận hàng"
          >
            <legend>Hình thức nhận hàng</legend>
            <div className="checkout-radio-group">
              <label
                className={`checkout-radio-option ${
                  fulfillmentType === 'Pickup' ? 'selected' : ''
                }`}
              >
                <input
                  type="radio"
                  name="fulfillment"
                  value="Pickup"
                  checked={fulfillmentType === 'Pickup'}
                  onChange={() => setFulfillmentType('Pickup')}
                  disabled={isSubmitting || paymentOptions?.onlineEnabled !== true}
                />
                <div className="checkout-radio-content">
                  <span className="checkout-radio-title">Nhận tại chi nhánh</span>
                  <span className="checkout-radio-desc">
                    Miễn phí - Nhận hàng trực tiếp tại siêu thị
                  </span>
                </div>
              </label>

              <label
                className={`checkout-radio-option ${
                  fulfillmentType === 'Delivery' ? 'selected' : ''
                }`}
              >
                <input
                  type="radio"
                  name="fulfillment"
                  value="Delivery"
                  checked={fulfillmentType === 'Delivery'}
                  onChange={() => setFulfillmentType('Delivery')}
                  disabled={isSubmitting}
                />
                <div className="checkout-radio-content">
                  <span className="checkout-radio-title">Giao hàng tận nơi</span>
                  <span className="checkout-radio-desc">
                    Phí giao hàng: {formatPrice(DELIVERY_SHIPPING_FEE)}
                  </span>
                </div>
              </label>
            </div>

            {fulfillmentType === 'Delivery' && (
              <div className="checkout-delivery-fields">
                {addresses.length > 0 && (
                  <div className="checkout-field">
                    <label htmlFor="saved-address-select">Địa chỉ đã lưu</label>
                    <select
                      id="saved-address-select"
                      value={selectedAddressId}
                      onChange={(e) => handleSavedAddressChange(e.target.value)}
                      disabled={isSubmitting}
                    >
                      {addresses.map((addr) => (
                        <option key={addr.id} value={addr.id}>
                          {addr.recipientName} - {addr.phone} ({formatAddress(addr)})
                          {addr.isDefault ? ' [Mặc định]' : ''}
                        </option>
                      ))}
                    </select>
                  </div>
                )}

                {addressLoadError && (
                  <p className="checkout-alert checkout-alert--info" role="status">
                    {addressLoadError}
                  </p>
                )}

                <div className="checkout-field">
                  <label htmlFor="recipient-name">Người nhận</label>
                  <input
                    id="recipient-name"
                    type="text"
                    required
                    value={recipientName}
                    onChange={(e) => setRecipientName(e.target.value)}
                    placeholder="Nguyễn Văn A"
                    disabled={isSubmitting}
                  />
                </div>

                <div className="checkout-field">
                  <label htmlFor="recipient-phone">Số điện thoại</label>
                  <input
                    id="recipient-phone"
                    type="tel"
                    required
                    value={recipientPhone}
                    onChange={(e) => setRecipientPhone(e.target.value)}
                    placeholder="0901234567"
                    disabled={isSubmitting}
                  />
                </div>

                <div className="checkout-field">
                  <label htmlFor="delivery-address">Địa chỉ giao hàng</label>
                  <input
                    id="delivery-address"
                    type="text"
                    required
                    value={deliveryAddress}
                    onChange={(e) => setDeliveryAddress(e.target.value)}
                    placeholder="Số nhà, tên đường, phường/xã, quận/huyện, tỉnh/thành"
                    disabled={isSubmitting}
                  />
                </div>
              </div>
            )}
          </fieldset>

          <fieldset
            className="checkout-form__section"
            role="group"
            aria-label="Phương thức thanh toán"
          >
            <legend>Phương thức thanh toán</legend>
            <div className="checkout-radio-group">
              <label
                className={`checkout-radio-option ${
                  paymentMethod === 'COD' ? 'selected' : ''
                }`}
              >
                <input
                  type="radio"
                  name="paymentMethod"
                  value="COD"
                  checked={paymentMethod === 'COD'}
                  onChange={() => setPaymentMethod('COD')}
                  disabled={isSubmitting}
                />
                <div className="checkout-radio-content">
                  <span className="checkout-radio-title">
                    Thanh toán khi nhận hàng (COD)
                  </span>
                  <span className="checkout-radio-desc">
                    Thanh toán tiền mặt hoặc quẹt thẻ khi nhận hàng
                  </span>
                </div>
              </label>

              <label
                className={`checkout-radio-option ${
                  paymentMethod === 'VNPay' ? 'selected' : ''
                }`}
              >
                <input
                  type="radio"
                  name="paymentMethod"
                  value="VNPay"
                  checked={paymentMethod === 'VNPay'}
                  onChange={() => setPaymentMethod('VNPay')}
                  disabled={isSubmitting || paymentOptions?.onlineEnabled !== true}
                />
                <div className="checkout-radio-content">
                  <span className="checkout-radio-title">VNPay giả lập — không thu tiền</span>
                  <span className="checkout-radio-desc">
                    Chọn kết quả thanh toán ngay trong ứng dụng.
                  </span>
                </div>
              </label>

              <label
                className={`checkout-radio-option ${
                  paymentMethod === 'MoMo' ? 'selected' : ''
                }`}
              >
                <input
                  type="radio"
                  name="paymentMethod"
                  value="MoMo"
                  checked={paymentMethod === 'MoMo'}
                  onChange={() => setPaymentMethod('MoMo')}
                  disabled={isSubmitting || paymentOptions?.onlineEnabled !== true}
                />
                <div className="checkout-radio-content">
                  <span className="checkout-radio-title">MoMo giả lập — không thu tiền</span>
                  <span className="checkout-radio-desc">
                    Chọn kết quả thanh toán ngay trong ứng dụng.
                  </span>
                </div>
              </label>
            </div>
            {paymentOptions?.onlineEnabled === false && (
              <p className="checkout-alert" role="status">
                Thanh toán online chưa khả dụng: {paymentOptions.disabledReason ?? 'PAYMENT_PROVIDER_NOT_CONFIGURED'}
              </p>
            )}
          </fieldset>

          <fieldset
            className="checkout-form__section"
            role="group"
            aria-label="Mã giảm giá"
          >
            <legend>Mã giảm giá</legend>
            {appliedCoupon ? (
              <div className="checkout-coupon-applied" role="status">
                <span>
                  Đã áp dụng <strong>{appliedCoupon.code}</strong> — giảm{' '}
                  {formatPrice(appliedCoupon.discountAmount)}
                </span>
                <button
                  type="button"
                  className="checkout-btn checkout-btn--ghost"
                  onClick={handleRemoveCoupon}
                  disabled={isSubmitting || !!createdOrderId}
                >
                  Gỡ mã
                </button>
              </div>
            ) : (
              <div className="checkout-coupon-row">
                <input
                  type="text"
                  aria-label="Mã giảm giá"
                  placeholder="Nhập mã giảm giá"
                  value={couponInput}
                  onChange={(e) => setCouponInput(e.target.value)}
                  disabled={isSubmitting || couponChecking || !!createdOrderId}
                />
                <button
                  type="button"
                  className="checkout-btn checkout-btn--secondary"
                  onClick={handleApplyCoupon}
                  disabled={isSubmitting || couponChecking || !couponInput.trim() || !!createdOrderId}
                >
                  {couponChecking ? 'Đang kiểm tra...' : 'Áp dụng'}
                </button>
              </div>
            )}
            {couponError && (
              <p className="checkout-alert checkout-alert--info" role="alert">
                {couponError}
              </p>
            )}
          </fieldset>

          {submitError && (
            <div className="checkout-alert" role="alert">
              <p>{submitError}</p>
              {submitError.includes('Giỏ hàng không còn sản phẩm') && (
                <Link to="/shopping/cart">Quay lại giỏ hàng</Link>
              )}
            </div>
          )}

          <button
            type="submit"
            className="checkout-btn checkout-btn--primary checkout-submit-btn"
            disabled={isSubmitting}
          >
            {isSubmitting
              ? 'Đang xử lý...'
              : createdOrderId
              ? 'Thử lại thanh toán'
              : 'Đặt hàng'}
          </button>
        </form>

        <CheckoutOrderSummary
          cart={cart}
          shippingFee={shippingFee}
          discount={appliedCoupon?.discountAmount ?? 0}
        />
      </div>
    </section>
  )
}
