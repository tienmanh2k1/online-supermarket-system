import { getJson } from './httpClient'

export interface OrderListDto {
  id: string
  createdAtUtc: string
  totalAmount: number
  status: string
  fulfillmentType: string
  itemCount: number
}

export interface OrderItemDto {
  orderItemId?: string
  productId: string
  productName: string
  sku: string
  unitPrice: number
  quantity: number
  lineTotal: number
  canReview?: boolean
  reviewId?: string | null
}

export interface StatusHistoryDto {
  fromStatus: string
  toStatus: string
  note: string | null
  createdAtUtc: string
}

export interface PaymentDto {
  id: string
  method: string
  status: string
  amount: number
  providerTransactionId: string | null
  createdAtUtc: string
  isMock?: boolean
}

export interface OrderDetailDto {
  id: string
  userId: string
  branchId: string
  fulfillmentType: string
  recipientName: string
  recipientPhone: string
  deliveryAddressSnapshot: string
  subtotal: number
  discountAmount: number
  shippingFee: number
  totalAmount: number
  promotionCodeSnapshot: string | null
  status: string
  createdAtUtc: string
  updatedAtUtc: string
  items: OrderItemDto[]
  statusHistory: StatusHistoryDto[]
  payment: PaymentDto | null
}

export interface PaginatedOrdersDto {
  data: OrderListDto[]
  totalCount: number
  page: number
  pageSize: number
}

export interface GetOrdersParams {
  status?: string
  page?: number
  pageSize?: number
}

function buildOrdersQuery(params: GetOrdersParams) {
  const search = new URLSearchParams()
  search.set('page', String(params.page ?? 1))
  search.set('pageSize', String(params.pageSize ?? 10))
  if (params.status) search.set('status', params.status)
  return search.toString()
}

export const orderApi = {
  getOrders: (params: GetOrdersParams, token: string, signal?: AbortSignal) =>
    getJson<PaginatedOrdersDto>('/orders?' + buildOrdersQuery(params), { token, signal }),
  getOrderById: (id: string, token: string, signal?: AbortSignal) =>
    getJson<OrderDetailDto>('/orders/' + encodeURIComponent(id), { token, signal }),
}
