import { getJson, postJson } from './httpClient'

export type FulfillmentType = 'Pickup' | 'Delivery'
export type PaymentMethod = 'COD' | 'VNPay' | 'MoMo'

export interface CheckoutRequest {
  fulfillmentType: FulfillmentType
  deliveryAddressId?: string | null
  recipientName?: string | null
  recipientPhone?: string | null
  deliveryAddress?: string | null
  couponCode?: string | null
}

export interface CouponValidationRequest {
  code: string
}

export interface CouponValidationResponse {
  valid: boolean
  discountAmount: number
  reason: string | null
  message: string
}

export interface PaymentInitDto {
  paymentId: string
  method: string
  status: string
  checkoutUrl: string | null
  isMock?: boolean
}

export interface CheckoutResponse {
  orderId: string
  subtotal: number
  discountAmount: number
  shippingFee: number
  totalAmount: number
  status: string
  payment: PaymentInitDto | null
}

export interface PaymentRequest {
  orderId: string
  method: PaymentMethod
}

export interface PaymentOptionsDto {
  mode: 'Mock' | 'Sandbox'
  onlineEnabled: boolean
  disabledReason: string | null
}

export const checkoutApi = {
  checkout: (data: CheckoutRequest, token: string, signal?: AbortSignal) =>
    postJson<CheckoutResponse>('/checkout', data, { token, signal }),
  initiatePayment: (data: PaymentRequest, token: string, signal?: AbortSignal) =>
    postJson<PaymentInitDto>('/checkout/payment', data, { token, signal }),
  getPaymentOptions: (token: string, signal?: AbortSignal) =>
    getJson<PaymentOptionsDto>('/checkout/payment-options', { token, signal }),
  validateCoupon: (data: CouponValidationRequest, token: string, signal?: AbortSignal) =>
    postJson<CouponValidationResponse>('/checkout/validate-coupon', data, { token, signal }),
}
