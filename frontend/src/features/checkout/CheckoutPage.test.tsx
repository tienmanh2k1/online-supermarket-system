import React from 'react'
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { CheckoutPage } from './CheckoutPage'
import { useAuth } from '../auth/AuthContext'
import { useCart, type CartContextValue } from '../cart/CartContext'
import { checkoutApi, type CheckoutResponse } from '../../api/checkoutApi'
import * as addressApi from '../../api/addressApi'
import type { AddressDto } from '../../api/addressApi'
import { ApiError } from '../../api/httpClient'
import type { CartDto } from '../../api/cartApi'

vi.mock('../auth/AuthContext', () => ({ useAuth: vi.fn() }))
vi.mock('../cart/CartContext', () => ({ useCart: vi.fn() }))

const mockNavigate = vi.fn()
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  }
})

const useAuthMock = vi.mocked(useAuth)
const useCartMock = vi.mocked(useCart)

const baseCart: CartDto = {
  id: 'cart-1',
  userId: 'user-1',
  branchId: 'branch-1',
  items: [],
  totalItems: 0,
  subtotal: 0,
}

const cartWithItems: CartDto = {
  id: 'cart-1',
  userId: 'user-1',
  branchId: 'branch-1',
  totalItems: 1,
  subtotal: 100000,
  items: [
    {
      id: 'item-1',
      productId: 'prod-1',
      productName: 'Điện thoại A',
      sku: 'DT-A',
      unitPrice: 100000,
      quantity: 1,
      lineTotal: 100000,
      availableQuantity: 5,
    },
  ],
}

const defaultAuth: ReturnType<typeof useAuth> = {
  user: { id: 'user-1', email: 'user@example.com', fullName: 'Nguyen Van A', role: 'Customer' },
  accessToken: 'jwt-token',
  isAuthenticated: true,
  isLoading: false,
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
  updateUser: vi.fn(),
  refreshUser: vi.fn(),
}

const defaultCartContext: CartContextValue = {
  status: 'ready',
  cart: cartWithItems,
  errorMessage: null,
  mutatingItemIds: new Set(),
  isAddingItem: false,
  isChangingBranch: false,
  isClearing: false,
  reloadCart: vi.fn().mockResolvedValue(undefined),
  addItem: vi.fn(),
  updateItemQuantity: vi.fn(),
  removeItem: vi.fn(),
  changeBranch: vi.fn(),
  clearCart: vi.fn(),
}

const checkoutResponse: CheckoutResponse = {
  orderId: 'order-1',
  subtotal: 100000,
  discountAmount: 0,
  shippingFee: 0,
  totalAmount: 100000,
  status: 'Pending',
  payment: null,
}

function renderCheckout({
  auth = defaultAuth,
  cart = cartWithItems,
  reloadCart = vi.fn().mockResolvedValue(undefined),
  status = 'ready' as const,
}: {
  auth?: Partial<typeof defaultAuth>
  cart?: CartDto | null
  reloadCart?: () => Promise<void>
  status?: 'idle' | 'loading' | 'ready' | 'error'
} = {}) {
  useAuthMock.mockReturnValue({ ...defaultAuth, ...auth })
  useCartMock.mockReturnValue({ ...defaultCartContext, status, cart, reloadCart })

  return render(
    <MemoryRouter initialEntries={['/shopping/checkout']}>
      <Routes>
        <Route path="/shopping/checkout" element={<CheckoutPage />} />
      </Routes>
    </MemoryRouter>
  )
}

describe('CheckoutPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.spyOn(addressApi, 'getAddressesApi').mockResolvedValue([])
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('allows pickup with COD when online payments are disabled', async () => {
    vi.spyOn(checkoutApi, 'getPaymentOptions').mockResolvedValue({ mode: 'Sandbox', onlineEnabled: false, disabledReason: 'Online unavailable' })
    renderCheckout()
    await screen.findByText(/Online unavailable/)
    const pickup = screen.getByRole('radio', { name: /Nhận tại chi nhánh/ })
    expect(pickup).toBeEnabled()
    fireEvent.click(pickup)
    expect(pickup).toBeChecked()
  })

  it('asks guests to login before checkout even when cart state is idle', () => {
    renderCheckout({
      auth: { isAuthenticated: false, isLoading: false, user: null, accessToken: null },
      status: 'idle',
      cart: null,
    })
    expect(screen.getByRole('heading', { name: 'Đăng nhập để thanh toán' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Đăng nhập' })).toBeInTheDocument()
  })

  it('sends empty carts back to shopping cart', () => {
    renderCheckout({ cart: baseCart })
    expect(screen.getByRole('heading', { name: 'Giỏ hàng đang trống' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Quay lại giỏ hàng' })).toHaveAttribute(
      'href',
      '/shopping/cart'
    )
  })

  it('renders the checkout heading and order summary for a ready cart', () => {
    renderCheckout({ cart: cartWithItems })
    expect(screen.getByRole('heading', { level: 1, name: 'Thanh toán đơn hàng' })).toBeInTheDocument()
    expect(screen.getByText('Điện thoại A')).toBeInTheDocument()
    expect(screen.getAllByText('100.000 ₫').length).toBeGreaterThan(0)
  })

  it('defaults to pickup with zero shipping fee', () => {
    renderCheckout({ cart: cartWithItems })
    expect(screen.getByRole('radio', { name: /Nhận tại chi nhánh/ })).toBeChecked()
    expect(screen.getByText('Phí giao hàng')).toBeInTheDocument()
    expect(screen.getAllByText('0 ₫').length).toBeGreaterThan(0)
  })

  it('shows delivery fields and adds the backend shipping fee', async () => {
    renderCheckout({ cart: cartWithItems })

    fireEvent.click(screen.getByRole('radio', { name: /Giao hàng tận nơi/ }))

    expect(screen.getByLabelText('Người nhận')).toBeInTheDocument()
    expect(screen.getByLabelText('Số điện thoại')).toBeInTheDocument()
    expect(screen.getByLabelText('Địa chỉ giao hàng')).toBeInTheDocument()
    expect(screen.getByText('15.000 ₫')).toBeInTheDocument()
    expect(screen.getByText('115.000 ₫')).toBeInTheDocument()
  })

  it('loads saved addresses and pre-fills the selected address', async () => {
    const mockAddresses: AddressDto[] = [
      {
        id: 'address-1',
        recipientName: 'Nguyen Van A',
        phone: '0900000000',
        street: '12 Nguyen Trai',
        ward: 'Ben Thanh',
        district: 'Quan 1',
        city: 'TP.HCM',
        postalCode: null,
        isDefault: true,
        createdAtUtc: '2026-08-28T00:00:00Z',
        updatedAtUtc: '2026-08-28T00:00:00Z',
      },
    ]
    vi.spyOn(addressApi, 'getAddressesApi').mockResolvedValue(mockAddresses)

    renderCheckout({ cart: cartWithItems })
    fireEvent.click(screen.getByRole('radio', { name: /Giao hàng tận nơi/ }))

    expect(await screen.findByDisplayValue('Nguyen Van A')).toBeInTheDocument()
    expect(screen.getByDisplayValue('0900000000')).toBeInTheDocument()
    expect(screen.getByDisplayValue('12 Nguyen Trai, Ben Thanh, Quan 1, TP.HCM')).toBeInTheDocument()
  })

  it('blocks delivery submission when recipient data is missing', async () => {
    const checkoutSpy = vi.spyOn(checkoutApi, 'checkout')
    renderCheckout({ cart: cartWithItems })

    fireEvent.click(screen.getByRole('radio', { name: /Giao hàng tận nơi/ }))
    fireEvent.change(screen.getByLabelText('Người nhận'), { target: { value: '' } })
    fireEvent.click(screen.getByRole('button', { name: 'Đặt hàng' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Vui lòng nhập đầy đủ thông tin giao hàng.')
    expect(checkoutSpy).not.toHaveBeenCalled()
  })

  it('creates an order, initiates COD payment, reloads cart and navigates to success', async () => {
    const checkoutSpy = vi.spyOn(checkoutApi, 'checkout').mockResolvedValue(checkoutResponse)
    const paymentSpy = vi.spyOn(checkoutApi, 'initiatePayment').mockResolvedValue({
      paymentId: 'payment-1',
      method: 'COD',
      status: 'PendingCollection',
      checkoutUrl: null,
    })
    const reloadCart = vi.fn().mockResolvedValue(undefined)

    renderCheckout({ cart: cartWithItems, reloadCart })
    fireEvent.click(screen.getByRole('radio', { name: /Thanh toán khi nhận hàng/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Đặt hàng' }))

    await waitFor(() => {
      expect(checkoutSpy).toHaveBeenCalledWith({ fulfillmentType: 'Pickup' }, 'jwt-token', expect.any(AbortSignal))
      expect(paymentSpy).toHaveBeenCalledWith({ orderId: 'order-1', method: 'COD' }, 'jwt-token', expect.any(AbortSignal))
      expect(reloadCart).toHaveBeenCalled()
      expect(mockNavigate).toHaveBeenCalledWith('/shopping/checkout/success?orderId=order-1&paymentStatus=PendingCollection')
    })
  })

  it('navigates to the internal mock checkout for VNPay', async () => {
    vi.spyOn(checkoutApi, 'getPaymentOptions').mockResolvedValue({ mode: 'Mock', onlineEnabled: true, disabledReason: null })
    vi.spyOn(checkoutApi, 'checkout').mockResolvedValue(checkoutResponse)
    vi.spyOn(checkoutApi, 'initiatePayment').mockResolvedValue({
      paymentId: 'payment-1', method: 'VNPay', status: 'Pending',
      checkoutUrl: '/shopping/payment/mock/payment-1', isMock: true,
    })
    renderCheckout({ cart: cartWithItems })
    const vnpay = screen.getByRole('radio', { name: /VNPay/ })
    await waitFor(() => expect(vnpay).toBeEnabled())
    fireEvent.click(vnpay)
    fireEvent.click(screen.getByRole('button', { name: 'Đặt hàng' }))
    await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith('/shopping/payment/mock/payment-1'))
  })
  it('shows cart empty error and links back to cart', async () => {
    vi.spyOn(checkoutApi, 'checkout').mockRejectedValue(new ApiError(400, { message: 'CART_EMPTY' }))
    renderCheckout({ cart: cartWithItems })

    fireEvent.click(screen.getByRole('button', { name: 'Đặt hàng' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Giỏ hàng không còn sản phẩm.')
    expect(screen.getByRole('link', { name: 'Quay lại giỏ hàng' })).toHaveAttribute(
      'href',
      '/shopping/cart'
    )
  })

  it('shows stock conflict error from backend', async () => {
    vi.spyOn(checkoutApi, 'checkout').mockRejectedValue(new ApiError(409, { message: 'INSUFFICIENT_STOCK' }))
    renderCheckout({ cart: cartWithItems })

    fireEvent.click(screen.getByRole('button', { name: 'Đặt hàng' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Một số sản phẩm không còn đủ tồn kho.')
  })

  it('retains created orderId and retries payment without creating duplicate order', async () => {
    const checkoutSpy = vi.spyOn(checkoutApi, 'checkout').mockResolvedValue({
      orderId: 'order-999',
      subtotal: 100000,
      discountAmount: 0,
      shippingFee: 0,
      totalAmount: 100000,
      status: 'Pending',
      payment: null,
    })
    const paymentSpy = vi
      .spyOn(checkoutApi, 'initiatePayment')
      .mockRejectedValueOnce(new ApiError(500, { message: 'GATEWAY_TIMEOUT' }))
      .mockResolvedValueOnce({
        paymentId: 'payment-999',
        method: 'COD',
        status: 'PendingCollection',
        checkoutUrl: null,
      })
    const reloadCart = vi.fn().mockResolvedValue(undefined)

    renderCheckout({ cart: cartWithItems, reloadCart })
    fireEvent.click(screen.getByRole('radio', { name: /Thanh toán khi nhận hàng/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Đặt hàng' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Đơn hàng (order-999) đã được tạo nhưng chưa thể khởi tạo thanh toán. Vui lòng chọn lại phương thức và thử lại.'
    )
    expect(checkoutSpy).toHaveBeenCalledTimes(1)
    expect(paymentSpy).toHaveBeenCalledTimes(1)

    const retryBtn = screen.getByRole('button', { name: 'Thử lại thanh toán' })
    expect(retryBtn).toBeInTheDocument()
    fireEvent.click(retryBtn)

    await waitFor(() => {
      expect(checkoutSpy).toHaveBeenCalledTimes(1)
      expect(paymentSpy).toHaveBeenCalledTimes(2)
      expect(paymentSpy).toHaveBeenLastCalledWith(
        { orderId: 'order-999', method: 'COD' },
        'jwt-token',
        expect.any(AbortSignal)
      )
      expect(mockNavigate).toHaveBeenCalledWith(
        '/shopping/checkout/success?orderId=order-999&paymentStatus=PendingCollection'
      )
    })
  })

  it('proceeds to success even if reloadCart fails after successful payment', async () => {
    vi.spyOn(checkoutApi, 'checkout').mockResolvedValue(checkoutResponse)
    vi.spyOn(checkoutApi, 'initiatePayment').mockResolvedValue({
      paymentId: 'payment-1',
      method: 'COD',
      status: 'PendingCollection',
      checkoutUrl: null,
    })
    const failingReloadCart = vi.fn().mockRejectedValue(new Error('Network error during reload'))

    renderCheckout({ cart: cartWithItems, reloadCart: failingReloadCart })
    fireEvent.click(screen.getByRole('radio', { name: /Thanh toán khi nhận hàng/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Đặt hàng' }))

    await waitFor(() => {
      expect(failingReloadCart).toHaveBeenCalled()
      expect(mockNavigate).toHaveBeenCalledWith(
        '/shopping/checkout/success?orderId=order-1&paymentStatus=PendingCollection'
      )
    })
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('exposes accessible checkout form controls', () => {
    renderCheckout({ cart: cartWithItems })
    expect(screen.getByRole('form', { name: 'Thông tin thanh toán' })).toBeInTheDocument()
    expect(screen.getByRole('group', { name: 'Hình thức nhận hàng' })).toBeInTheDocument()
    expect(screen.getByRole('group', { name: 'Phương thức thanh toán' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Đặt hàng' })).toBeEnabled()
  })

  it('applies a valid coupon and shows the discount in the summary', async () => {
    vi.spyOn(checkoutApi, 'validateCoupon').mockResolvedValue({
      valid: true,
      discountAmount: 10000,
      reason: null,
      message: 'Mã giảm giá hợp lệ.',
    })
    renderCheckout({ cart: cartWithItems })

    fireEvent.change(screen.getByPlaceholderText('Nhập mã giảm giá'), { target: { value: 'sale10' } })
    fireEvent.click(screen.getByRole('button', { name: 'Áp dụng' }))

    expect(await screen.findByText('SALE10')).toBeInTheDocument()
    expect(screen.getByText('Giảm giá')).toBeInTheDocument()
    expect(screen.getByText('-10.000 ₫')).toBeInTheDocument()
    // subtotal 100.000 - 10.000 discount + 0 shipping = 90.000
    expect(screen.getByText('90.000 ₫')).toBeInTheDocument()
  })

  it('shows the backend message when a coupon is invalid', async () => {
    vi.spyOn(checkoutApi, 'validateCoupon').mockResolvedValue({
      valid: false,
      discountAmount: 0,
      reason: 'MIN_ORDER_NOT_MET',
      message: 'Đơn hàng chưa đạt giá trị tối thiểu để áp mã.',
    })
    renderCheckout({ cart: cartWithItems })

    fireEvent.change(screen.getByPlaceholderText('Nhập mã giảm giá'), { target: { value: 'BIG' } })
    fireEvent.click(screen.getByRole('button', { name: 'Áp dụng' }))

    expect(await screen.findByText('Đơn hàng chưa đạt giá trị tối thiểu để áp mã.')).toBeInTheDocument()
    expect(screen.queryByText('Giảm giá')).not.toBeInTheDocument()
  })

  it('removes an applied coupon and reverts the summary', async () => {
    vi.spyOn(checkoutApi, 'validateCoupon').mockResolvedValue({
      valid: true,
      discountAmount: 10000,
      reason: null,
      message: 'ok',
    })
    renderCheckout({ cart: cartWithItems })

    fireEvent.change(screen.getByPlaceholderText('Nhập mã giảm giá'), { target: { value: 'SALE10' } })
    fireEvent.click(screen.getByRole('button', { name: 'Áp dụng' }))
    await screen.findByText('SALE10')

    fireEvent.click(screen.getByRole('button', { name: 'Gỡ mã' }))

    expect(screen.queryByText('Giảm giá')).not.toBeInTheDocument()
    expect(screen.getByPlaceholderText('Nhập mã giảm giá')).toBeInTheDocument()
  })

  it('includes the applied coupon code in the checkout request', async () => {
    vi.spyOn(checkoutApi, 'validateCoupon').mockResolvedValue({
      valid: true,
      discountAmount: 10000,
      reason: null,
      message: 'ok',
    })
    const checkoutSpy = vi.spyOn(checkoutApi, 'checkout').mockResolvedValue(checkoutResponse)
    vi.spyOn(checkoutApi, 'initiatePayment').mockResolvedValue({
      paymentId: 'pay-1',
      method: 'COD',
      status: 'PendingCollection',
      checkoutUrl: null,
    })
    renderCheckout({ cart: cartWithItems })

    fireEvent.change(screen.getByPlaceholderText('Nhập mã giảm giá'), { target: { value: 'sale10' } })
    fireEvent.click(screen.getByRole('button', { name: 'Áp dụng' }))
    await screen.findByText('SALE10')

    fireEvent.click(screen.getByRole('button', { name: 'Đặt hàng' }))

    await waitFor(() => {
      expect(checkoutSpy).toHaveBeenCalledWith(
        { fulfillmentType: 'Pickup', couponCode: 'SALE10' },
        'jwt-token',
        expect.any(AbortSignal)
      )
    })
  })
})
